using Grpc.Core;

namespace TransactionManager;

public class TransactionService : TransactionManagerService.TransactionManagerServiceBase
{
    private Dictionary<string, Int32> _internalDB;

    public TransactionService()
    {
        _internalDB = new Dictionary<string, int>();
    }

    public override Task<TxSubmitResponse> TxSubmit(TxSubmitRequest request, ServerCallContext context)
    {
        var response = new TxSubmitResponse();
        // Acquire Leases

        
        // Write values
        foreach (var entry in request.WriteEntries)
        {
            _internalDB.Add(entry.Key, entry.Value);
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