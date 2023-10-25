using Shared;

namespace LeaseManager;


public class PrepareBroadcastMethods : BroadcastMethods {

    private int _responses;
    private bool _hasMajority;

    public PrepareBroadcastMethods(LogManager logManager, List<object> clients) : base(logManager, clients)
    {
        _responses = 1;
    }

    public void ResetResponsesCounter() {
        //1 to account for the request sent to the server it self
        _responses = 1;
    }

    public override async Task<object> Send(int index, object request) {

        var client = _clients[index] as LeaseManagerService.LeaseManagerServiceClient;

        try {
            return (object) await client!.PrepareAsync(request as PrepareRequest);
        } catch (Exception e) {
            e.Data.Add("ClientIndex", index);
            throw;
        }
    }

    public override bool ReceiveResponse(object response)
    {
        var responseCast = response as Promise;
        _logManager.Logger.Debug("[Prepare Broadcast (URB)]: Received response {@0}", responseCast);

        if (responseCast != null && responseCast.Status == PaxosResponseStatus.Accept)
        {
            _responses += 1;
        }

        _hasMajority = (_responses > (_clients.Count / 2));

        return _hasMajority;
    }

    public bool HasMajority => _hasMajority;
}

public class AcceptBroadcastMethods : BroadcastMethods {

    private int _responses;
    private bool _hasMajority;

    public AcceptBroadcastMethods(LogManager logManager, List<object> clients) : base(logManager, clients)
    {
        _responses = 1;
    }

    public void ResetResponsesCounter() {
        //1 to account for the request sent to the server it self
        _responses = 1;
    }

    public override async Task<object> Send(int index, object request) {

        var client = _clients[index] as LeaseManagerService.LeaseManagerServiceClient;

        try {
            return (object) await client!.AcceptAsync(request as AcceptRequest);
        } catch (Exception e) {
            e.Data.Add("ClientIndex", index);
            throw;
        }
    }

    public override bool ReceiveResponse(object response)
    {
        var responseCast = response as Accepted;
        _logManager.Logger.Debug("[Accepted Broadcast (URB)]: Received response {@0}", responseCast);

        if (responseCast != null && responseCast.Status == PaxosResponseStatus.Accept)
        {
            _responses += 1;
        }

        _hasMajority = (_responses > (_clients.Count / 2));

        return _hasMajority;
    }

    public bool HasMajority => _hasMajority;
}

public class PaxosBroadcast : BroadcastClient
{
    // RPC Broadcast Definitions
    private PrepareBroadcastMethods _prepareRPC; 
    private AcceptBroadcastMethods _acceptRPC;
    
    public PaxosBroadcast(ConfigurationManager configurationManager, LogManager logManager) : base(configurationManager, logManager, ServerType.Lease, true)
    {
        var _clients = new List<object>();
        
        foreach (var channel in _channels)
        {
            _clients.Add(new LeaseManagerService.LeaseManagerServiceClient(channel));
        }

        _prepareRPC = new PrepareBroadcastMethods(logManager, _clients);
        _acceptRPC = new AcceptBroadcastMethods(logManager, _clients);
    }

    public async Task<List<Promise>> PrepareURB(PrepareRequest request) {
        _prepareRPC.ResetResponsesCounter();
        List<object> promisesObjs = await base.UniformReliableBroadcast(_prepareRPC, request as object);

        return promisesObjs.Cast<Promise>().ToList(); 
    }

    public async Task<List<Accepted>> AcceptURB(AcceptRequest request) {
        _acceptRPC.ResetResponsesCounter();
        List<object> acceptObjs = await base.UniformReliableBroadcast(_acceptRPC, request as object);

        return acceptObjs.Cast<Accepted>().ToList(); 
    }

    public bool PrepareHasMajority => _prepareRPC.HasMajority;
    public bool AcceptHasMajority => _acceptRPC.HasMajority;

}