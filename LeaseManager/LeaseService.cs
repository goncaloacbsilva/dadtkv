using Grpc.Core;
using Shared;

namespace LeaseManager;

public class LeaseService : LeaseManagerService.LeaseManagerServiceBase
{
    private LogManager _logManager;

    public LeaseService(LogManager logManager)
    {
        _logManager = logManager;
    }

    public override Task<LeaseResponse> Request(LeaseRequest request, ServerCallContext context)
    {
        _logManager.Logger.Debug("Received: {0}", request);
        return base.Request(request, context);
    }
}