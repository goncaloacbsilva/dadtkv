using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Shared;

namespace LeaseManager;

struct PaxosState {
    public int writeTimestamp;
    public int readTimestamp;
    public string lastPromiseIdentifier;
    public List<LeaseRequest> value;
}

public class LeaseService : LeaseManagerService.LeaseManagerServiceBase
{
    
    private ConfigurationManager _configurationManager;
    private LogManager _logManager;
    private readonly PaxosBroadcast _paxosBroadcast;
    private readonly LeasesBroadcast _leasesBroadcast;
    private PaxosState _state;
    private Queue<Tuple<int, LeaseRequest>> _pendingRequests;


    public LeaseService(ConfigurationManager configurationManager, LogManager logManager)
    {
        _configurationManager = configurationManager;
        _logManager = logManager;
        _paxosBroadcast = new PaxosBroadcast(configurationManager, logManager);
        _leasesBroadcast = new LeasesBroadcast(configurationManager, logManager);

        _state.value = new List<LeaseRequest>();
        _pendingRequests = new Queue<Tuple<int, LeaseRequest>>();
        
        _configurationManager.NextSlotEvent += (sender, args) => { RunPaxos(); };
    }

    public override Task<LeaseRequestResponse> Request(LeaseRequest request, ServerCallContext context)
    {
        _logManager.Logger.Debug("Received: {@0}", request);
        _pendingRequests.Enqueue(Tuple.Create(_configurationManager.CurrentEpoch, request));
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
        if (request.Epoch > _state.readTimestamp || (request.Epoch == _state.readTimestamp && string.Compare(request.Identifier, _configurationManager.Identifier) > 0)) {
            _logManager.Logger.Debug("[Paxos]: Updated read timestamp epoch to: {0}", request.Epoch);
            _state.readTimestamp = request.Epoch;
            _state.lastPromiseIdentifier = request.Identifier;
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

        _logManager.Logger.Debug("[Paxos]: Received accept request {@0}", request);

        if (request.Epoch == _state.readTimestamp && request.Identifier.Equals(_state.lastPromiseIdentifier))
        {
            response.Status = PaxosResponseStatus.Accept;
            _state.writeTimestamp = request.Epoch;
            _state.value = request.Value.ToList();
            _logManager.Logger.Debug("[Paxos]: Updated write timestamp and value epoch to: {0}", request.Epoch);
        }
        else
        {
            response.Status = PaxosResponseStatus.Reject;
            _logManager.Logger.Debug("[Paxos]: Accept rejected");
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

    // For swaping Txs
    public static void Swap<T>(IList<T> list, int indexA, int indexB)
    {
        T tmp = list[indexA];
        list[indexA] = list[indexB];
        list[indexB] = tmp;
    }

    class LeasesTable
    {
        Dictionary<string, string> leases;

        public LeasesTable()
        {
            leases = new Dictionary<string, string>();
        }

        public void UpdateTable(LeaseRequest request)
        {
            foreach (var obj in request.Objects)
            {
                if (leases.TryAdd(obj, request.TmIdentifier))
                {
                    leases[obj] = request.TmIdentifier;
                }
            }
        }

        public bool HasConflicts(LeaseRequest request, string prevID)
        {
            foreach (var obj in request.Objects)
            {
                string value;
                if (leases.TryGetValue(obj, out value))
                {
                    if (value.Equals(prevID))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    // Recreate timeline without consecutive Tm leases
    private List<LeaseRequest> RewriteTxsTimeline(List<LeaseRequest> leases)
    {
        List<LeaseRequest> withoutConsecutives = new List<LeaseRequest>(leases);
        LeasesTable leasesTable = new LeasesTable();


        int consecutives = 0;
        LeaseRequest prevRequest = null;
        for (int i = 0; i < withoutConsecutives.Count; i++)
        {
            if (prevRequest == null || !prevRequest.TmIdentifier.Equals(withoutConsecutives[i].TmIdentifier))
            {
                if (prevRequest != null && leasesTable.HasConflicts(withoutConsecutives[i], prevRequest.TmIdentifier) && consecutives > 0)
                {
                    Swap(withoutConsecutives, i - consecutives, i);
                    i = i - consecutives + 1;
                }
                consecutives = 0;
            }
            else
            {
                consecutives++;
            }
            prevRequest = withoutConsecutives[i];
            leasesTable.UpdateTable(withoutConsecutives[i]);
        }

        return withoutConsecutives;
    }

    private List<LeaseRequest> ProcessPendingRequests() {
        Tuple<int, LeaseRequest> request;
        List<LeaseRequest> newValue = new List<LeaseRequest>();

        _logManager.Logger.Debug("[Paxos]: Dequeuing request from epoch {0}", _configurationManager.CurrentEpoch - 1);
        if (_pendingRequests.Any())
        {
            
            while (_pendingRequests.TryDequeue(out request) && request.Item1 <= _configurationManager.CurrentEpoch - 1) {
                if (request.Item1 == _configurationManager.CurrentEpoch - 1)
                {
                    newValue.Add(request.Item2);
                }
            }

            newValue.Sort((x, y) => DateTime.Compare(x.Timestamp.ToDateTime(), y.Timestamp.ToDateTime()));
        }
        _logManager.Logger.Debug("[Paxos]: New value {@0}", newValue);

        return newValue;
    }

    public async void RunPaxos() {

        _state.value.Clear();

        if (_configurationManager.CurrentState == ServerState.Crashed) {
            Environment.Exit(0);
        }

        // Check if leader is alive with suspects
        if (CurrentIsLeader())
        {
            _logManager.Logger.Information("[Paxos]: Im a leader, starting epoch {0}", _configurationManager.CurrentEpoch);
            _state.readTimestamp = _configurationManager.CurrentEpoch;
            _state.lastPromiseIdentifier = _configurationManager.Identifier;
            // Prepare
            _logManager.Logger.Information("[Paxos]: Sending prepare requests...");
            var promises = await _paxosBroadcast.BroadcastWithPhase<PrepareRequest, Promise>(new PrepareRequest {
                Epoch = _configurationManager.CurrentEpoch,
                Identifier = _configurationManager.Identifier
            }, BroadcastPhase.Prepare);

            // Check if there were promises with accepted values
            var highestAcceptedValue = HighestAcceptedValue(promises);
            
            if (highestAcceptedValue.Any()) {
                _state.value = highestAcceptedValue;
            } else {
                // My own value will be used
                _logManager.Logger.Debug("[Paxos]: No previous accepted values were found, using my own value");
                _state.value = RewriteTxsTimeline(ProcessPendingRequests());
                _logManager.Logger.Information("[RewriteTxsTimeline]: {@0}", _state.value);
            }

            // Accept
            _logManager.Logger.Information("[Paxos]: Sending accept requests...");

            var request = new AcceptRequest
            {
                Epoch = _configurationManager.CurrentEpoch,
                Identifier = _configurationManager.Identifier
            };
            request.Value.Add(_state.value);
            var accept = await _paxosBroadcast.BroadcastWithPhase<AcceptRequest, Accepted>(request, BroadcastPhase.Accept);

            if (_paxosBroadcast.HasMajority) {
                _logManager.Logger.Information("[Paxos]: Finished! The choosen value is: {@0}", _state.value);
                
                // Broadcast choosen value
                var leases = new Leases();
                leases.Leases_.Add(getLeasesOrdered(_state.value));
                _leasesBroadcast.Broadcast<Leases, LeasesResponse>(leases);
            }

        }
        else
        {
            _logManager.Logger.Debug("[Paxos]: Im not a leader");
        }
    }
    
    private Dictionary<string, ObjectLeases> getLeasesOrdered(List<LeaseRequest> leases)
    {
        Dictionary<string, ObjectLeases> dict = new Dictionary<string, ObjectLeases>();

        foreach (var item in leases)
        {
            foreach (var obj in item.Objects)
            {
                if (!dict.ContainsKey(obj))
                {
                    dict.Add(obj, new ObjectLeases());
                }
                
                dict[obj].TmIdentifiers.Add(item.TmIdentifier);
            }           
        }

        return dict;
    }
    
}