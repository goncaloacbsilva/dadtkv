using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Shared;
using System.Collections.Generic;
using System.Linq;

namespace TransactionManager;


struct Transaction
{
    public TxSubmitRequest request;
    public HashSet<string> requiredLeases;
}

public class TransactionService : TransactionManagerService.TransactionManagerServiceBase
{
    private Dictionary<string, Int32> _internalDB;
    private Dictionary<string, Queue<string>> _leases;
    private Queue<Transaction> _pendingTransactions;
    
    private readonly ConfigurationManager _configurationManager;
    private LogManager _logManager;
    private readonly TMBroadcast _tmBroadcast;
    
    public TransactionService(ConfigurationManager configurationManager, LogManager logManager)
    {
        _internalDB = new Dictionary<string, int>();
        _leases = new Dictionary<string, Queue<string>>();
        _pendingTransactions = new Queue<Transaction>();
        
        _tmBroadcast = new TMBroadcast(configurationManager, logManager);
        _configurationManager = configurationManager;
        _logManager = logManager;
    }

    private bool HasLease(string key)
    {
        if (!_leases.ContainsKey(key)) return false;
        return (_leases[key].TryPeek(out var tmIdentifier) && tmIdentifier.Equals(_configurationManager.Identifier));
    }
    
    private HashSet<string> GetObjects(TxSubmitRequest request)
    {
        var objects = new HashSet<string>();
        
        request.WriteEntries.ToList().ForEach(entry => objects.Add(entry.Key));
        request.ReadEntries.ToList().ForEach(key => objects.Add(key));
        
        return objects;
    }


    private void ExecuteTx(Transaction tx) {
        var request = tx.request;

        // Write values
        foreach (var entry in request.WriteEntries)
        {
            if (_internalDB.TryAdd(entry.Key, entry.Value))
            {
                _internalDB[entry.Key] = entry.Value;
            }
            _logManager.Logger.Debug("Leases State: object updated to {@0}", entry);
        }
        
        // Read values
        /* foreach (var key in request.ReadEntries)
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
        } */
    }

    private void ProcessTransactions() {

        _logManager.Logger.Debug("Checking Leases and executing transactions...");
        _logManager.Logger.Debug("Leases State: {@0}", _leases);
        while (_pendingTransactions.Any() && !_pendingTransactions.Peek().requiredLeases.Where(key => !HasLease(key)).ToList().Any()) {
            // Execute Transaction
            ExecuteTx(_pendingTransactions.Dequeue());
        }
    }

    public override Task<LeasesResponse> UpdateLeases(Leases leases, ServerCallContext context)
    {
        var response = new LeasesResponse();

        _logManager.Logger.Debug("Updating leases");

        //add new leases to object queues
        foreach (var lease in leases.Leases_)
        {
            if (!_leases.ContainsKey(lease.Key))
            {
                _leases.Add(lease.Key, new Queue<string>());
            }
            foreach(var value in lease.Value.TmIdentifiers)
            {
                if (!_leases[lease.Key].Any() || !value.Equals(_leases[lease.Key].Last()))
                    _leases[lease.Key].Enqueue(value);
            }
                
        }

        ProcessTransactions();

        return Task.FromResult(response);
    }

    public override Task<TxSubmitResponse> TxSubmit(TxSubmitRequest request, ServerCallContext context)
    {
        var response = new TxSubmitResponse();

        _logManager.Logger.Debug("Received TxSubmit from {0}", request.ClientId);

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
            _tmBroadcast.Broadcast<LeaseRequest, LeaseRequestResponse>(leaseRequest);
        }

        _pendingTransactions.Enqueue(new Transaction
        {
            request = request,
            requiredLeases = requiredObjects
        });

        return Task.FromResult(response);
    }

    public override Task<StatusResponse> Status(TxStatusRequest request, ServerCallContext context)
    {
        return base.Status(request, context);
    }
}