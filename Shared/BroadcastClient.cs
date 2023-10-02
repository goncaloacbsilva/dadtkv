using Grpc.Core;
using Serilog.Core;

namespace Shared;

public abstract class BroadcastClient
{
    protected List<Channel> _channels;
    private LogManager _logManager;

    protected BroadcastClient(ConfigurationManager configurationManager, LogManager logManager, ServerType typeSelector)
    {
        _channels = new List<Channel>();
        _logManager = logManager;

        _logManager.Logger.Debug("[Broadcast Client]: Type selector is {0}", typeSelector.ToString());
        
        // Init channels
        foreach (var serverEntry in configurationManager.Servers.Where(server => server.type == typeSelector).ToList())
        {
            _logManager.Logger.Debug("[Broadcast Client]: Connecting to {0}:{1}", serverEntry.host, serverEntry.port);
            _channels.Add(new Channel(serverEntry.host, serverEntry.port, ChannelCredentials.Insecure));
        }
    }

    public abstract void Send<T>(int index, T request);

    public void Broadcast<T>(T request)
    {
        for (int i = 0; i < _channels.Count; i++)
        {
            _logManager.Logger.Debug("[Broadcast Client]: Broadcasting to {0}", _channels[i].Target);
            Send(i, request);
        }
    }

    public void ShutdownChannels()
    {
        foreach (var channel in _channels)
        {
            channel.ShutdownAsync().Wait();
        }
    }
}