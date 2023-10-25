using Shared;

public class SyncBroadcastMethods : BroadcastMethods {

    private int _responses;
    private bool _hasMajority;

    public SyncBroadcastMethods(LogManager logManager, List<object> clients) : base(logManager, clients)
    {
        _responses = 1;
    }

    public void ResetResponsesCounter() {
        //1 to account for the request sent to the server it self
        _responses = 1;
    }

    public override async Task<object> Send(int index, object request) {

        var client = _clients[index] as TransactionManagerService.TransactionManagerServiceClient;

        try {
            return (object) await client!.SyncTransactionAsync(request as SyncRequest);
        } catch (Exception e) {
            e.Data.Add("ClientIndex", index);
            throw;
        }
    }

    public override bool ReceiveResponse(object response)
    {
        var responseCast = response as SyncResponse;
        _logManager.Logger.Debug("[SYNC Broadcast (URB)]: Received response {@0}", responseCast);

        if (responseCast != null && responseCast.Status)
        {
            _responses += 1;
        }

        _hasMajority = (_responses > (_clients.Count / 2));

        return _hasMajority;
    }

    public bool HasMajority => _hasMajority;
}

public class TMStatusRequestBroadcastMethods : BroadcastMethods {
    public TMStatusRequestBroadcastMethods(LogManager logManager, List<object> clients) : base(logManager, clients)
    {
    }

    public override async Task<object> Send(int index, object request) {

        var client = _clients[index] as TransactionManagerService.TransactionManagerServiceClient;

        return (object) await client!.StatusAsync(request as StatusRequest);
    }

    public override bool ReceiveResponse(object response)
    {
        // We wont run URB when Broadcasting leases so no need to define this handler
        throw new NotImplementedException();
    }
}

public class TMBroadcast : BroadcastClient
{
    // RPC Broadcast Definitions
    private SyncBroadcastMethods _syncRPC; 
    private TMStatusRequestBroadcastMethods _statusRPC;
    
    public TMBroadcast(ConfigurationManager configurationManager, LogManager logManager) : base(configurationManager, logManager, ServerType.Transaction, true)
    {
        var _clients = new List<object>();

        foreach (var channel in base._channels)
        {
            _clients.Add(new TransactionManagerService.TransactionManagerServiceClient(channel));
        }

        _syncRPC = new SyncBroadcastMethods(logManager, _clients);
        _statusRPC = new TMStatusRequestBroadcastMethods(logManager, _clients);
    }

    public async Task<List<SyncResponse>> SyncURB(SyncRequest request) {
        _syncRPC.ResetResponsesCounter();
        List<object> syncObjs = await base.UniformReliableBroadcast(_syncRPC, request as object);

        return syncObjs.Cast<SyncResponse>().ToList();
    }

    public async Task BroadcastStatus() {
        await base.Broadcast(_statusRPC, (new StatusRequest {
            IsFromClient = false,
        }) as object);
    }

    public bool SyncHasMajority => _syncRPC.HasMajority;
}