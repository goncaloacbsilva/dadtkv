using Shared;

public class UpdateLeasesBroadcastMethods : BroadcastMethods {
    public UpdateLeasesBroadcastMethods(LogManager logManager, List<object> clients) : base(logManager, clients)
    {
    }

    public override async Task<object> Send(int index, object request) {

        var client = _clients[index] as TransactionManagerService.TransactionManagerServiceClient;

        return (object) await client!.UpdateLeasesAsync(request as Leases);
    }

    public override bool ReceiveResponse(object response)
    {
        // We wont run URB when Broadcasting leases so no need to define this handler
        throw new NotImplementedException();
    }
}

public class LeasesBroadcast : BroadcastClient
{

    // RPC Broadcast Definitions
    private UpdateLeasesBroadcastMethods _updateLeasesRPC; 
    
    public LeasesBroadcast(ConfigurationManager configurationManager, LogManager logManager) : base(configurationManager, logManager, ServerType.Transaction, false)
    {
        var _clients = new List<object>();
        
        foreach (var channel in base._channels)
        {
            _clients.Add(new TransactionManagerService.TransactionManagerServiceClient(channel));
        }

        _updateLeasesRPC = new UpdateLeasesBroadcastMethods(logManager, _clients);
    }

    public async Task Broadcast(Leases leases) {
        await base.Broadcast(_updateLeasesRPC, leases as object);
    }
    
}