using Shared;

public class TMBroadcast : BroadcastClient
{
    private readonly List<TransactionManagerService.TransactionManagerServiceClient> _clients;
    
    public TMBroadcast(ConfigurationManager configurationManager, LogManager logManager) : base(configurationManager, logManager, ServerType.Transaction, true)
    {
        _clients = new List<TransactionManagerService.TransactionManagerServiceClient>();
        
        foreach (var channel in base._channels)
        {
            _clients.Add(new TransactionManagerService.TransactionManagerServiceClient(channel));
        }
    }

    public override async Task<TResponse> Send<TRequest, TResponse>(int index, TRequest request)
    {
        return (TResponse) Convert.ChangeType(await _clients[index].SyncTransactionAsync(request as SyncRequest), typeof(TResponse)); 
    }
}