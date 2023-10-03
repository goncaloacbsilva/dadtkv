using Grpc.Core;
using Shared;

namespace LeaseManager;

public class LeaseService : LeaseManagerService.LeaseManagerServiceBase
{
    
    private ConfigurationManager _configurationManager;
    private LogManager _logManager;


    public LeaseService(ConfigurationManager configurationManager, LogManager logManager)
    {
        _configurationManager = configurationManager;
        _logManager = logManager;

        _configurationManager.NextSlotEvent += (sender, args) => { TestFunction(); };
    }

    public override Task<LeaseResponse> Request(LeaseRequest request, ServerCallContext context)
    {
        _logManager.Logger.Debug("Received: {@0}", request);
        return base.Request(request, context);
    }

    public async void TestFunction() {
        _logManager.Logger.Debug("Test function trigered!!");
    }
}