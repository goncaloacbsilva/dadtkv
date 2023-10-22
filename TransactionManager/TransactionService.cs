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

    private bool NextTxNeeds(string obj) {
        _logManager.Logger.Debug("Transaction Objects: {@0}", _pendingTransactionsObjects);
        if (_pendingTransactionsObjects.Count > 1) {
            var tx = _pendingTransactionsObjects.ElementAt(1);
            return tx.requiredLeases.Contains(obj);
        }

        return false;
    }

    // Returns (<objects to dequeue>, <objects to request again>)
    private (HashSet<string>, HashSet<string>) FreeLeases(Transaction tx) {
        HashSet<string> leasesToDequeue = new HashSet<string>();
        HashSet<string> requestObjects = new HashSet<string>();

        foreach (var key in tx.requiredLeases) {
            if (_leases[key].Count > 1) {
                _leases[key].Dequeue();
                leasesToDequeue.Add(key);

                if (NextTxNeeds(key)) {
                    requestObjects.Add(key);
                }
            }
        }

        return (leasesToDequeue, requestObjects);
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

        (var leasesToDequeue, var requestObjects) = FreeLeases(tx);

        _logManager.Logger.Information("[ExecTx]: {@0}", _leases);

        _logManager.Logger.Debug("[Transaction]: Leases to Dequeue {@0}", leasesToDequeue);
        _logManager.Logger.Debug("[Transaction]: Leases to Request Again {@0}", requestObjects);

        var syncRequest = new SyncRequest();
        var leaseRequest = new LeaseRequest
        {
            TmIdentifier = _configurationManager.Identifier,
            Timestamp = Timestamp.FromDateTime(DateTime.UtcNow)
        };


        leaseRequest.Objects.Add(requestObjects);
        syncRequest.UpdateEntries.Add(request.WriteEntries);
        syncRequest.DequeueObjects.Add(leasesToDequeue);

        _logManager.Logger.Information("[Transaction]: SYNC TX Broadcast: {@0}", syncRequest);
        _logManager.Logger.Information("[Transaction]: REQUEST LEASES Broadcast: {@0}", leaseRequest);

        // Broadcast sync transaction request
        _transactionSyncBroadcast.Broadcast<SyncRequest, SyncResponse>(syncRequest);
        _leaseRequestBroadcast.Broadcast<LeaseRequest, LeaseRequestResponse>(leaseRequest);

        return response;
    }

    public override Task<LeasesResponse> UpdateLeases(Leases leases, ServerCallContext context)
    {
        var response = new LeasesResponse();

        _logManager.Logger.Information("[Update Leases Request]: {@0}", leases.Leases_);

        foreach (var lease in leases.Leases_)
        {
            if (!_leases.ContainsKey(lease.Key))
            {
                _leases.Add(lease.Key, new Queue<string>());
            }
            bool isfirst = true;
            foreach (var value in lease.Value.TmIdentifiers)
            {

                if (isfirst && _leases[lease.Key].Any() && !value.Equals(_leases[lease.Key].Last()))
                {
                    _leases[lease.Key].Dequeue();
                    _leases[lease.Key].Enqueue(value);

                    isfirst = false;
                }
                else if ((_leases[lease.Key].Any() && !value.Equals(_leases[lease.Key].Last()))
                    || !_leases[lease.Key].Any())
                {
                    _leases[lease.Key].Enqueue(value);
                    isfirst = false;
                }
            }

        }
        _logManager.Logger.Information("[Current Leases State]: {@0}", _leases);

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


        var response = await tcs.Task;

        _logManager.Logger.Information("[TX RESULT RESPONSE]: {@0}", response);

        return response;
    }

    public override Task<StatusResponse> Status(TxStatusRequest request, ServerCallContext context)
    {
        return base.Status(request, context);
    }
}