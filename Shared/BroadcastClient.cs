using Grpc.Core;
namespace Shared;

public abstract class BroadcastClient
{
    protected List<Channel> _channels;
    
    protected BroadcastClient(ConfigurationManager configurationManager, ServerType typeSelector)
    {
        _channels = new List<Channel>();
        
        Console.WriteLine("[Broadcast Client]: Type selector is {0}", typeSelector.ToString());
        
        // Init channels
        foreach (var serverEntry in configurationManager.Servers.Where(server => server.type == typeSelector).ToList())
        {
            Console.WriteLine("[Broadcast Client]: Connecting to {0}:{1}", serverEntry.host, serverEntry.port);
            _channels.Add(new Channel(serverEntry.host, serverEntry.port, ChannelCredentials.Insecure));
        }
    }

    public abstract void Send<T>(int index, T request);

    public void Broadcast<T>(T request)
    {
        for (int i = 0; i < _channels.Count; i++)
        {
            Console.WriteLine("[Broadcast Client]: Broadcasting to {0}", _channels[i].Target);
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