using Shared;

public class TMBroadcast : BroadcastClient
{
    private readonly List<LeaseManagerService.LeaseManagerServiceClient> _clients;
    
    public TMBroadcast(ConfigurationManager configurationManager, LogManager logManager) : base(configurationManager, logManager, ServerType.Lease, false)
    {
        _clients = new List<LeaseManagerService.LeaseManagerServiceClient>();
        
        foreach (var channel in base._channels)
        {
            _clients.Add(new LeaseManagerService.LeaseManagerServiceClient(channel));
        }
    }

    public override void Send<T>(int index, T request)
    {
        _clients[index].RequestAsync(request as LeaseRequest);
    }
}