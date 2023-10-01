using Grpc.Core;

namespace LeaseManager;

public class LeaseService : LeaseManagerService.LeaseManagerServiceBase
{
    public override Task<LeaseResponse> Request(LeaseRequest request, ServerCallContext context)
    {
        Console.WriteLine("Received: {0}", request);
        return base.Request(request, context);
    }
}