using Grpc.Core;
using Shared;

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
                TmIdentifier = _configurationManager.Identifier
            };

            leaseRequest.Objects.Add(missingLeases);
            _tmBroadcast.Broadcast(leaseRequest);
        }

        _pendingTransactions.Enqueue(new Transaction
        {
            request = request,
            requiredLeases = requiredObjects
        });


        // Write values
        foreach (var entry in request.WriteEntries)
        {
            if (_internalDB.TryAdd(entry.Key, entry.Value))
            {
                _internalDB[entry.Key] = entry.Value;
            }
        }

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
                Console.WriteLine("Error: Key not found: {0}", key);
            }
        }
        
        return Task.FromResult(response);
    }

    public override Task<StatusResponse> Status(TxStatusRequest request, ServerCallContext context)
    {
        return base.Status(request, context);
    }
}