using Shared;

public class LeaseRequestBroadcastMethods : BroadcastMethods {
    public LeaseRequestBroadcastMethods(LogManager logManager, List<object> clients) : base(logManager, clients)
    {
    }

    public override async Task<object> Send(int index, object request) {

        var client = _clients[index] as LeaseManagerService.LeaseManagerServiceClient;

        return (object) await client!.RequestAsync(request as LeaseRequest);
    }

    public override bool ReceiveResponse(object response)
    {
        // We wont run URB when Broadcasting leases so no need to define this handler
        throw new NotImplementedException();
    }
}

public class StatusRequestBroadcastMethods : BroadcastMethods {
    public StatusRequestBroadcastMethods(LogManager logManager, List<object> clients) : base(logManager, clients)
    {
    }

    public override async Task<object> Send(int index, object request) {

        var client = _clients[index] as LeaseManagerService.LeaseManagerServiceClient;

        return (object) await client!.StatusAsync(request as LMStatusRequest);
    }

    public override bool ReceiveResponse(object response)
    {
        // We wont run URB when Broadcasting leases so no need to define this handler
        throw new NotImplementedException();
    }
}

public class LeaseRequestBroadcast : BroadcastClient
{

    // RPC Broadcast Definitions
    private LeaseRequestBroadcastMethods _leaseRequestRPC; 
    private StatusRequestBroadcastMethods _statusRequest;
    
    public LeaseRequestBroadcast(ConfigurationManager configurationManager, LogManager logManager) : base(configurationManager, logManager, ServerType.Lease, false)
    {
        var _clients = new List<object>();
        
        foreach (var channel in base._channels)
        {
            _clients.Add(new LeaseManagerService.LeaseManagerServiceClient(channel));
        }

        _leaseRequestRPC = new LeaseRequestBroadcastMethods(logManager, _clients);
        _statusRequest = new StatusRequestBroadcastMethods(logManager, _clients);
    }

    public async Task Broadcast(LeaseRequest request) {
        await base.Broadcast(_leaseRequestRPC, request as object);
    }

    public async Task BroadcastStatus() {
        await base.Broadcast(_statusRequest, (new LMStatusRequest()) as object);
    }
}