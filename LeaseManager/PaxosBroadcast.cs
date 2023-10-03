using Shared;

namespace LeaseManager;

public enum BroadcastPhase
{
    Prepare,
    Accept,
    Commit
}

public class PaxosBroadcast : BroadcastClient
{
    private readonly List<LeaseManagerService.LeaseManagerServiceClient> _clients;
    private BroadcastPhase _phase;
    
    public PaxosBroadcast(ConfigurationManager configurationManager, LogManager logManager) : base(configurationManager, logManager, ServerType.Lease, true)
    {
        _clients = new List<LeaseManagerService.LeaseManagerServiceClient>();
        
        foreach (var channel in _channels)
        {
            _clients.Add(new LeaseManagerService.LeaseManagerServiceClient(channel));
        }
    }

    public void BroadcastWithPhase<T>(T request, BroadcastPhase phase)
    {
        _phase = phase;
        Broadcast(request);
    }

    public override void Send<T>(int index, T request)
    {
        switch (_phase)
        {
            case BroadcastPhase.Prepare:
                _clients[index].Prepare(request as PrepareRequest);
                break;
            default:
                throw new Exception("[Paxos Broadcast]: Error: Phase not implemented");
        }
    }
}