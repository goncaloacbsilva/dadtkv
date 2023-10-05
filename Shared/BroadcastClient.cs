using Grpc.Core;
using Serilog.Core;

namespace Shared;

public abstract class BroadcastClient
{
    protected List<Channel> _channels;
    private LogManager _logManager;

    protected BroadcastClient(ConfigurationManager configurationManager, LogManager logManager, ServerType typeSelector, bool excludeSelf)
    {
        _channels = new List<Channel>();
        _logManager = logManager;

        _logManager.Logger.Debug("[Broadcast Client]: Type selector is {0}", typeSelector.ToString());
        
        // Init channels
        foreach (var serverEntry in configurationManager.Servers.Where(server => (server.type == typeSelector) && (!excludeSelf || !server.identifier.Equals(configurationManager.Identifier))).ToList())
        {
            _logManager.Logger.Debug("[Broadcast Client]: Connecting to {0}:{1}", serverEntry.host, serverEntry.port);
            _channels.Add(new Channel(serverEntry.host, serverEntry.port, ChannelCredentials.Insecure));
        }
    }

    public abstract Task<TResponse> Send<TRequest, TResponse>(int index, TRequest request);

    public void Broadcast<TRequest, TResponse>(TRequest request)
    {
        for (int i = 0; i < _channels.Count; i++)
        {
            _logManager.Logger.Debug("[Broadcast]: Broadcasting to {0}", _channels[i].Target);
            Send<TRequest, TResponse>(i, request);
        }
    }

    public async Task<List<TResponse>> UniformReliableBroadcast<TRequest, TResponse>(TRequest request, Func<TResponse, bool> hasMajority) {
        List<Task<TResponse>> sendTasks = new List<Task<TResponse>>();

        
        for (int i = 0; i < _channels.Count; i++)
        {
            _logManager.Logger.Debug("[Broadcast (URB)]: Broadcasting to {0}", _channels[i].Target);
            sendTasks.Add(Send<TRequest, TResponse>(i, request));
        }

        // Wait for responses
        while (sendTasks.Any())
        {
            Task<TResponse> finishedTask = await Task.WhenAny(sendTasks);
            sendTasks.Remove(finishedTask);
            _logManager.Logger.Debug("[Broadcast (URB)]: Received response {@0}", finishedTask.Result);
            if (hasMajority(finishedTask.Result)) { break; }
        }

        return sendTasks.Select(task => task.Result).ToList();
    }

    public void ShutdownChannels()
    {
        foreach (var channel in _channels)
        {
            channel.ShutdownAsync().Wait();
        }
    }
}