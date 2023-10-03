using Grpc.Core;
using Shared;

namespace LeaseManager;

public class LeaseService : LeaseManagerService.LeaseManagerServiceBase
{
    
    private ConfigurationManager _configurationManager;
    private LogManager _logManager;
    private Queue<Tuple<int, LeaseRequest>> _queue;


    public LeaseService(ConfigurationManager configurationManager, LogManager logManager)
    {
        _queue = new Queue<Tuple<int, LeaseRequest>>();
        _configurationManager = configurationManager;
        _logManager = logManager;

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
        try
        {
            var currentSuspected = _configurationManager.CurrentSuspects.Select(suspect => suspect.suspected).ToList();
            var leaseServers = _configurationManager.Servers
                .Where(server =>
                    (server.type == ServerType.Lease) &&
                    String.CompareOrdinal(server.identifier, _configurationManager.Identifier) < 0)
                .Select(server => server.identifier).ToList();

            _logManager.Logger.Debug("[Paxos]: Servers that I'm watching: {0}", leaseServers);
            _logManager.Logger.Debug("[Paxos]: Suspected servers: {0}", currentSuspected);
            
            return leaseServers.All(currentSuspected.Contains);
        }
        catch (Exception e)
        {
            return false;
        }
    }

    public async void RunPaxos() {
        // Check if leader is alive with suspects
        if (CurrentIsLeader())
        {
            _logManager.Logger.Information("[Paxos]: Im a leader, starting epoch {0}", _configurationManager.CurrentEpoch);
        }
        else
        {
            _logManager.Logger.Debug("[Paxos]: Im not a leader");
        }
    }
}