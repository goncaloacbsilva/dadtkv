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

    private readonly PaxosBroadcast _paxosBroadcast;
    private PaxosState _state;


    public LeaseService(ConfigurationManager configurationManager, LogManager logManager)
    {
        _configurationManager = configurationManager;
        _logManager = logManager;
        _paxosBroadcast = new PaxosBroadcast(configurationManager, logManager);
        _state.value = new List<LeaseRequest>();
        
        _configurationManager.NextSlotEvent += (sender, args) => { RunPaxos(); };
    }

    public override Task<LeaseRequestResponse> Request(LeaseRequest request, ServerCallContext context)
    {
        _logManager.Logger.Debug("Received: {@0}", request);
        _state.value.Add(request);
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
            response.Status = PaxosResponseStatus.Accept;
            _state.writeTimestamp = request.Epoch;
            _state.value = request.Value.ToList();
            _logManager.Logger.Debug("[Paxos]: Updated write timestamp and value epoch to: {0}", request.Epoch);
        }
        else
        {
            response.Status = PaxosResponseStatus.Reject;
            _logManager.Logger.Debug("[Paxos]: accept denied");
        }

        return Task.FromResult(response);
    }

    private List<LeaseRequest> HighestAcceptedValue(List<Promise> promises) {
        List<LeaseRequest> acceptedValue = new List<LeaseRequest>();
        int highestTimestamp = 0;

        foreach(var promise in promises) {
            if (promise.Status == PaxosResponseStatus.Accept && promise.Value.Any() && promise.WriteTimestamp > highestTimestamp) {
                _logManager.Logger.Debug("[Paxos]: Found promise accepted value with greater timestamp ({0}, highest was {1})", promise.WriteTimestamp, highestTimestamp);
                highestTimestamp = promise.WriteTimestamp;
                acceptedValue = promise.Value.ToList();
            }
        }

        return acceptedValue;
    }

    public async void RunPaxos() {
        if (_configurationManager.CurrentState == ServerState.Crashed) { return; }

        // Check if leader is alive with suspects
        if (CurrentIsLeader())
        {
            _logManager.Logger.Information("[Paxos]: Im a leader, starting epoch {0}", _configurationManager.CurrentEpoch);

            // Prepare
            _logManager.Logger.Information("[Paxos]: Sending prepare requests...");
            var promises = await _paxosBroadcast.BroadcastWithPhase<PrepareRequest, Promise>(new PrepareRequest {
                Epoch = _configurationManager.CurrentEpoch
            }, BroadcastPhase.Prepare);

            // Check if there were promises with accepted values
            var highestAcceptedValue = HighestAcceptedValue(promises);
            
            if (highestAcceptedValue.Any()) {
                _state.Value = highestAcceptedValue;
            } else {
                // Propose my own value
                _logManager.Logger.Debug("[Paxos]: No previous accepted values were found, creating my own value");
                
            }


            // Order leases


            // Accept

        }
        else
        {
            _logManager.Logger.Debug("[Paxos]: Im not a leader");
        }
    }
}