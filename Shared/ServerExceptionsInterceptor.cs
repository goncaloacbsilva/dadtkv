using Microsoft.Extensions.Logging;
using Serilog.Core;

namespace Shared;

using Grpc.Core;
using Grpc.Core.Interceptors;

public class ServerExceptionsInterceptor : Interceptor
{
    private readonly Logger _logger;

    public ServerExceptionsInterceptor(Logger logger)
    {
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {

        try
        {
            return await continuation(request, context);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Error thrown by {context.Method}.");                
            throw;
        }
    }
   
}