using Grpc.Core;
using Shared;

namespace LeaseManager;

struct PaxosState {
    public int writeTimestamp;
    public int readTimestamp;
    public List<LeaseRequest> value;
}

public class LeaseService : LeaseManagerService.LeaseManagerServiceBase
{
    
    private ConfigurationManager _configurationManager;
    private LogManager _logManager;
    private Queue<Tuple<int, LeaseRequest>> _queue;

    private readonly PaxosBroadcast _paxosBroadcast;
    private PaxosState _state;


    public LeaseService(ConfigurationManager configurationManager, LogManager logManager)
    {
        _queue = new Queue<Tuple<int, LeaseRequest>>();
        _configurationManager = configurationManager;
        _logManager = logManager;
        _paxosBroadcast = new PaxosBroadcast(configurationManager, logManager);
        _state.value = new List<LeaseRequest>();
        
        _configurationManager.NextSlotEvent += (sender, args) => { RunPaxos(); };
    }

    public override Task<LeaseRequestResponse> Request(LeaseRequest request, ServerCallContext context)
    {
        _logManager.Logger.Debug("Received: {@0}", request);
        _queue.Enqueue(Tuple.Create(_configurationManager.CurrentEpoch, request));
        return base.Request(request, context); 
    }

    private bool CurrentIsLeader()
    {
        _logManager.Logger.Debug("[Paxos]: Am I a leader?");
        var leaseServers = _configurationManager.Servers
            .Where(server =>
                (server.type == ServerType.Lease) &&
                String.CompareOrdinal(server.identifier, _configurationManager.Identifier) < 0)
            .Select(server => server.identifier).ToList();
        
        _logManager.Logger.Debug("[Paxos]: Servers that I'm watching: {0}", leaseServers);
        
        try
        {
            var currentSuspected = _configurationManager.CurrentSuspects().Select(suspect => suspect.suspected).ToList();
            _logManager.Logger.Debug("[Paxos]: Suspected servers: {0}", currentSuspected);
            
            return leaseServers.All(currentSuspected.Contains);
        }
        catch (Exception e)
        {
            return (leaseServers.Count == 0);
        }
    }

    public override Task<Promise> Prepare(PrepareRequest request, ServerCallContext context)
    {
        _logManager.Logger.Debug("[Paxos]: Received prepare request {@0}", request);
        if (request.Epoch > _state.readTimestamp) {
            _logManager.Logger.Debug("[Paxos]: Updated read timestamp epoch to: {0}", request.Epoch);
            _state.readTimestamp = request.Epoch;
        }

        var promise = new Promise {
            WriteTimestamp = _state.writeTimestamp
        };

        promise.Value.Add(_state.value);

        return Task.FromResult(promise);
    }

    public override Task<Accepted> Accept(AcceptRequest request, ServerCallContext context)
    {
        var response = new Accepted();

        
        if(request.Epoch ==  _state.readTimestamp)
        {
            response.Type = Accepted.Types.AcceptedType.Accept;
            _state.writeTimestamp = request.Epoch;
            _state.value = request.Value.ToList();
        }
        else
        {
            response.Type = Accepted.Types.AcceptedType.Reject;
        }

        return Task.FromResult(response);
    }

    public async void RunPaxos() {
        if (_configurationManager.CurrentState == ServerState.Crashed) { return; }

        // Check if leader is alive with suspects
        if (CurrentIsLeader())
        {
            _logManager.Logger.Information("[Paxos]: Im a leader, starting epoch {0}", _configurationManager.CurrentEpoch);

            // Prepare
            var promises = await _paxosBroadcast.BroadcastWithPhase<PrepareRequest, Promise>(new PrepareRequest {
                Epoch = _configurationManager.CurrentEpoch
            }, BroadcastPhase.Prepare);

            // Order leases
         
            
            // Accept

        }
        else
        {
            _logManager.Logger.Debug("[Paxos]: Im not a leader");
        }
    }
}