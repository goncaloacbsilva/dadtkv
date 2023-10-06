using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Shared;
using System.Collections.Concurrent;

namespace TransactionManager;


public struct Transaction
{
    public TxSubmitRequest request;
    public HashSet<string> requiredLeases;
}

public class TransactionService : TransactionManagerService.TransactionManagerServiceBase
{
    private Dictionary<string, Int32> _internalDB;
    private Dictionary<string, Queue<string>> _leases;
    private BlockingCollection<ActionWithResult<TxSubmitResponse>> _pendingTransactions;
    private Queue<Transaction> _pendingTransactionsObjects;
    
    private readonly ConfigurationManager _configurationManager;
    private LogManager _logManager;

    private readonly LeaseRequestBroadcast _leaseRequestBroadcast;
    private readonly TMBroadcast _transactionSyncBroadcast;

    private readonly TransactionsWorker _transactionWorker;
    
    public TransactionService(ConfigurationManager configurationManager, LogManager logManager)
    {
        _internalDB = new Dictionary<string, int>();
        _leases = new Dictionary<string, Queue<string>>();
        _pendingTransactionsObjects = new Queue<Transaction>();
        _pendingTransactions = new BlockingCollection<ActionWithResult<TxSubmitResponse>>();
        _leaseRequestBroadcast = new LeaseRequestBroadcast(configurationManager, logManager);
        _transactionSyncBroadcast = new TMBroadcast(configurationManager, logManager);
        _configurationManager = configurationManager;
        _logManager = logManager;

        _transactionWorker = new TransactionsWorker(_pendingTransactions, _pendingTransactionsObjects, _logManager);
        
        Task.Run(() => _transactionWorker.Start());
    }

    private bool HasLease(string key)
    {
        if (!_leases.ContainsKey(key)) return false;
        return (_leases[key].TryPeek(out var tmIdentifier) && tmIdentifier.Equals(_configurationManager.Identifier));
    }

    private bool HasLeases(Transaction tx) {
        return !tx.requiredLeases.Where(key => !HasLease(key)).ToList().Any();
    }
    
    private HashSet<string> GetObjects(TxSubmitRequest request)
    {
        var objects = new HashSet<string>();
        
        request.WriteEntries.ToList().ForEach(entry => objects.Add(entry.Key));
        request.ReadEntries.ToList().ForEach(key => objects.Add(key));
        
        return objects;
    }


    private async Task AquireLeases(Transaction tx) {
        while (!HasLeases(tx)) {
            // Pooling interval
            await Task.Delay(1000);
        }
    }

    private HashSet<string> FreeLeases(Transaction tx) {
        HashSet<string> leasesToDequeue = new HashSet<string>();
        foreach (var key in tx.requiredLeases) {
            var nextTm = _leases[key].ElementAtOrDefault(1);
            if (nextTm != default(string))
            {
                _leases[key].Dequeue();
                leasesToDequeue.Add(key);
            }
        }

        return leasesToDequeue;
    }

    private void WriteObjects(List<DadInt> values) {
        foreach (var entry in values)
        {
            if (_internalDB.TryAdd(entry.Key, entry.Value))
            {
                _internalDB[entry.Key] = entry.Value;
            }
            _logManager.Logger.Debug("[Transaction]: object updated to {@0}", entry);
        }
    }

    private async Task<TxSubmitResponse> ExecuteTx(Transaction tx) {
        var request = tx.request;
        var response = new TxSubmitResponse();

        _logManager.Logger.Debug("[Transaction]: waiting for leases");

        // Await to aquire leases
        await AquireLeases(tx);

        // Read values
        foreach (var key in request.ReadEntries)
        {
            Int32 value;

            if (_internalDB.TryGetValue(key, out value))
            {
                response.Entries.Add(new DadInt
                {
                    Key = key,
                    Value = value
                });
            }
            else
            {
                _logManager.Logger.Warning("Key not found: {0}", key);
            }
        }

        // Write values
        WriteObjects(request.WriteEntries.ToList());

        _logManager.Logger.Debug("[Transaction]: Freeing leases");

        var leasesToDequeue = FreeLeases(tx);

        _logManager.Logger.Information("[ExecTx]: {@0}", _leases);

        var syncRequest = new SyncRequest();
        
        syncRequest.UpdateEntries.Add(request.WriteEntries);
        syncRequest.DequeueObjects.Add(leasesToDequeue);
        
        _logManager.Logger.Information("[Transaction]: SYNC TX Broadcast: {@0}", syncRequest);
        
        // Broadcast sync transaction request
        _transactionSyncBroadcast.Broadcast<SyncRequest, SyncResponse>(syncRequest);

        return response;
    }

    public override Task<LeasesResponse> UpdateLeases(Leases leases, ServerCallContext context)
    {
        var response = new LeasesResponse();

        _logManager.Logger.Debug("Updating leases");


        var oldLeases = new Dictionary<string, Queue<string>>(_leases);

        //add new leases to object queues eleminating objects
        //that are sequencially duplicated
        foreach (var lease in leases.Leases_)
        {
            if (!_leases.ContainsKey(lease.Key))
            {
                _leases.Add(lease.Key, new Queue<string>());
            }
            bool isfirst = true;
            foreach(var value in lease.Value.TmIdentifiers)
            {
                
                if (isfirst && _leases[lease.Key].Any() && !value.Equals(_leases[lease.Key].Last()))
                {
                    Console.WriteLine();
                    Console.WriteLine(value);
                    Console.WriteLine();
                    _leases[lease.Key].Dequeue();
                    _leases[lease.Key].Enqueue(value);

                    var syncRequest = new SyncRequest();
                    syncRequest.DequeueObjects.Add(lease.Key);

                    _logManager.Logger.Information("[UpdateLeases Conflict]: SYNC TX Broadcast: {@0}", syncRequest);

                    // Broadcast sync transaction request
                    _transactionSyncBroadcast.Broadcast<SyncRequest, SyncResponse>(syncRequest);

                    isfirst = false;
                }
                else if ((_leases[lease.Key].Any() && !value.Equals(_leases[lease.Key].Last())) 
                    || !_leases[lease.Key].Any())
                {
                    _leases[lease.Key].Enqueue(value);
                }
            }
                
        }
        _logManager.Logger.Information("[Update Leases]: {@0}", _leases);

        return Task.FromResult(response);
    }

    public override Task<SyncResponse> SyncTransaction(SyncRequest request, ServerCallContext context)
    {
        _logManager.Logger.Debug("[SYNC TX]: Received request: {@0}", request);
        WriteObjects(request.UpdateEntries.ToList());
        foreach (var key in request.DequeueObjects) {
            _logManager.Logger.Debug("[SYNC TX]: Dequeuing: {0}", key);
            _leases[key].TryDequeue(out var none);
        }
        _logManager.Logger.Information("[SYNC TX]: {@0}", _leases);
        return Task.FromResult(new SyncResponse());
    }

    public async override Task<TxSubmitResponse> TxSubmit(TxSubmitRequest request, ServerCallContext context)
    {

        _logManager.Logger.Debug("Received TxSubmit from {0}", request.ClientId);
        _logManager.Logger.Information("[TXSUBMIT]: {@0}", request);

        // Acquire Leases
        var requiredObjects = GetObjects(request);
        var missingLeases = requiredObjects.Where(key => !HasLease(key)).ToList();

        if (missingLeases.Count != 0)
        {
            var leaseRequest = new LeaseRequest
            {
                TmIdentifier = _configurationManager.Identifier,
                Timestamp = Timestamp.FromDateTime(DateTime.UtcNow)
            };

            leaseRequest.Objects.Add(missingLeases);
            _leaseRequestBroadcast.Broadcast<LeaseRequest, LeaseRequestResponse>(leaseRequest);
        }
        
        // Append transaction
        var tcs = new TaskCompletionSource<TxSubmitResponse>();
        var tx = new Transaction
        {
            request = request,
            requiredLeases = requiredObjects
        };
        var transactionTask = new ActionWithResult<TxSubmitResponse>(() => ExecuteTx(tx), tcs);

        _pendingTransactions.Add(transactionTask);
        _pendingTransactionsObjects.Enqueue(tx);

        return await tcs.Task;
    }

    public override Task<StatusResponse> Status(TxStatusRequest request, ServerCallContext context)
    {
        return base.Status(request, context);
    }
}