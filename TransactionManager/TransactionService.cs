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

        try
        {
            // Write values
            foreach (var entry in request.WriteEntries)
            {
                _internalDB.Add(entry.Key, entry.Value);
            }

            // Read values
            var readEntries = request.ReadEntries.Select(key => new DadInt { Key = key, Value = _internalDB[key] })
                .ToList();
            
            response.Entries.Add(readEntries);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        
        return Task.FromResult(response);
    }

    public override Task<StatusResponse> Status(TxStatusRequest request, ServerCallContext context)
    {
        return base.Status(request, context);
    }
}