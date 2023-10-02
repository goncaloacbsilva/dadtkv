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
    
    private ConfigurationManager _configurationManager;
    private TMBroadcast _tmBroadcast;
    private string _identifier;
    
    public TransactionService(ConfigurationManager configurationManager, string identifier)
    {
        _internalDB = new Dictionary<string, int>();
        _leases = new Dictionary<string, Queue<string>>();
        _configurationManager = configurationManager;
        _tmBroadcast = new TMBroadcast(configurationManager);
        _pendingTransactions = new Queue<Transaction>();
        _identifier = identifier;
    }

    private bool hasLease(string key)
    {
        if (_leases.ContainsKey(key))
        {
            string tmIdentifier;
            return (_leases[key].TryPeek(out tmIdentifier) && tmIdentifier.Equals(_identifier));
        }

        return false;
    }
    
    private HashSet<string> getObjects(TxSubmitRequest request)
    {
        HashSet<string> objects = new HashSet<string>();
        
        request.WriteEntries.ToList().ForEach(entry => objects.Add(entry.Key));
        request.ReadEntries.ToList().ForEach(key => objects.Add(key));
        
        return objects;
    } 

    public override Task<TxSubmitResponse> TxSubmit(TxSubmitRequest request, ServerCallContext context)
    {
        var response = new TxSubmitResponse();

        Console.WriteLine("[{0}][TM Manager]: Received TxSubmit from {1}", _identifier, request.ClientId);

        // Acquire Leases
        var requiredObjects = getObjects(request);
        var missingLeases = requiredObjects.Where(key => !hasLease(key)).ToList();

        if (missingLeases.Count != 0)
        {
            var leaseRequest = new LeaseRequest
            {
                TmIdentifier = _identifier
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