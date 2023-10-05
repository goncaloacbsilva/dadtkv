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

    public Task<List<TResponse>> BroadcastWithPhase<TRequest, TResponse>(TRequest request, BroadcastPhase phase)
    {
        _phase = phase;

        Func<TResponse, bool> hasMajorityFunction = (response) => true;
        
        int responses = 0;

        switch (phase) {
            case BroadcastPhase.Prepare:
                hasMajorityFunction = (response) =>
                {
                    responses += 1;
                    return (responses > (_clients.Count / 2));
                };
                break;
            case BroadcastPhase.Accept:
                hasMajorityFunction = (response) =>
                {
                    
                    var acceptResponse = response as Accepted;

                    if (acceptResponse.Type == Accepted.Types.AcceptedType.Accept)
                    {
                        responses += 1;
                    }
                    return (responses > (_clients.Count / 2));
                };
                break;
        }

        return UniformReliableBroadcast<TRequest, TResponse>(request, hasMajorityFunction);
    }

    public override async Task<TResponse> Send<TRequest, TResponse>(int index, TRequest request)
    {
        switch (_phase)
        {
            case BroadcastPhase.Prepare:
                return (TResponse) Convert.ChangeType(await _clients[index].PrepareAsync(request as PrepareRequest), typeof(TResponse));
            default:
                throw new Exception("[Paxos Broadcast]: Error: Phase not implemented");
        }
    }
}