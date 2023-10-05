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
    private bool _hasMajority;
    
    public PaxosBroadcast(ConfigurationManager configurationManager, LogManager logManager) : base(configurationManager, logManager, ServerType.Lease, true)
    {
        _hasMajority = false;
        _clients = new List<LeaseManagerService.LeaseManagerServiceClient>();
        
        foreach (var channel in _channels)
        {
            _clients.Add(new LeaseManagerService.LeaseManagerServiceClient(channel));
        }
    }

    public Task<List<TResponse>> BroadcastWithPhase<TRequest, TResponse>(TRequest request, BroadcastPhase phase)
    {
        _phase = phase;
        _hasMajority = false;
        
        int responses = 0;

        Func<TResponse, bool> checkMajorityFunction = (TResponse response) =>
        {
            PaxosResponseStatus status = PaxosResponseStatus.Reject;

            switch(_phase) {
                case BroadcastPhase.Prepare:
                    status = (response as Promise).Status;
                    break;
                case BroadcastPhase.Accept:
                    status = (response as Accepted).Status;
                    break;
                default:
                    throw new Exception("[Paxos Broadcast]: Error: Phase not implemented");
            }

            if (status == PaxosResponseStatus.Accept)
            {
                responses += 1;
            }

            _hasMajority = (responses > (_clients.Count / 2));

            return _hasMajority;
        };

        return UniformReliableBroadcast<TRequest, TResponse>(request, checkMajorityFunction);
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

    public bool HasMajority => _hasMajority;
}