using Shared;

public class TMBroadcast : BroadcastClient
{
    private readonly List<TransactionManagerService.TransactionManagerServiceClient> _clients;
    private bool _hasMajority;
    
    public TMBroadcast(ConfigurationManager configurationManager, LogManager logManager) : base(configurationManager, logManager, ServerType.Transaction, true)
    {
        _clients = new List<TransactionManagerService.TransactionManagerServiceClient>();
        _hasMajority = false;

        foreach (var channel in base._channels)
        {
            _clients.Add(new TransactionManagerService.TransactionManagerServiceClient(channel));
        }
    }

    public Task<List<TResponse>> ExecuteURB<TRequest, TResponse>(TRequest request)
    {
        _hasMajority = false;
        //1 to account for the request sent to the server it self
        int responses = 1;

        Func<TResponse, bool> checkMajorityFunction = (TResponse response) =>
        {
            bool status = false;

            if (response != null) {
                status = (response as SyncResponse).Status;
                if (status) {
                    responses += 1;
                }
            }

            _hasMajority = (responses > (_clients.Count / 2));

            return _hasMajority;
        };

        return UniformReliableBroadcast<TRequest, TResponse>(request, checkMajorityFunction);
    }

    public override async Task<TResponse> Send<TRequest, TResponse>(int index, TRequest request)
    {
        try {
            return (TResponse) Convert.ChangeType(await _clients[index].SyncTransactionAsync(request as SyncRequest), typeof(TResponse)); 
        } catch (Exception e) {
            _logManager.Logger.Error("[Sync URB]: Error: Server {0} not responding", _channels[index].Target);
            return default(TResponse);
        }
    }

    public bool HasMajority => _hasMajority;
}