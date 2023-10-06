using Shared;

public class LeaseRequestBroadcast : BroadcastClient
{
    private readonly List<LeaseManagerService.LeaseManagerServiceClient> _clients;
    
    public LeaseRequestBroadcast(ConfigurationManager configurationManager, LogManager logManager) : base(configurationManager, logManager, ServerType.Lease, false)
    {
        _clients = new List<LeaseManagerService.LeaseManagerServiceClient>();
        
        foreach (var channel in base._channels)
        {
            _clients.Add(new LeaseManagerService.LeaseManagerServiceClient(channel));
        }
    }

    public override async Task<TResponse> Send<TRequest, TResponse>(int index, TRequest request)
    {
        return (TResponse) Convert.ChangeType(await _clients[index].RequestAsync(request as LeaseRequest), typeof(TResponse)); 
    }
}