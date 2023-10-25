using Grpc.Core;
using Serilog.Core;

namespace Shared;

public abstract class BroadcastMethods {
    protected List<object> _clients;
    protected LogManager _logManager;

    public BroadcastMethods(LogManager logManager, List<object> clients) {
        _clients = clients;
        _logManager = logManager;
    }

    public abstract Task<object> Send(int index, object request);
    public abstract bool ReceiveResponse(object response);
}

public abstract class BroadcastClient
{
    protected List<Channel> _channels;
    protected LogManager _logManager;

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

    protected async Task Broadcast(BroadcastMethods broadcastMethods, object request)
    {
        for (int i = 0; i < _channels.Count; i++)
        {
            _logManager.Logger.Debug("[Broadcast]: Broadcasting to {0}", _channels[i].Target);
            broadcastMethods.Send(i, request);
        }
    }

    protected async Task<List<object>> UniformReliableBroadcast(BroadcastMethods broadcastMethods, object request) {


        List<Task<object>> sendTasks = new List<Task<object>>();

        
        for (int i = 0; i < _channels.Count; i++)
        {
            _logManager.Logger.Debug("[Broadcast (URB)]: Broadcasting to {0}", _channels[i].Target);
            sendTasks.Add(broadcastMethods.Send(i, request));
        }

        // Wait for responses
        while (sendTasks.Any())
        {
            Task<object> finishedTask = await Task.WhenAny(sendTasks);
            sendTasks.Remove(finishedTask);
            if(finishedTask != null && finishedTask.Exception == null)
            {
                _logManager.Logger.Debug("[Broadcast (URB)]: Received response {@0}", finishedTask.Result);
                if (broadcastMethods.ReceiveResponse(finishedTask.Result)) { break; }
            }
        }

        return sendTasks.Where(task => {
            try {
                return (task.Result != null);
            } catch (AggregateException e) {
                if (e.InnerExceptions.Count > 0 && e.InnerExceptions[0].Data.Contains("ClientIndex")) {
                    var index = (int)e.InnerExceptions[0].Data["ClientIndex"]!;
                    _logManager.Logger.Error("Error: Server {0} not responding", _channels[index].Target);
                } else {
                    _logManager.Logger.Error("Error: Server not responding and unable to get its address!");
                    _logManager.Logger.Error("Exception: {@0}", e);
                }

                return false;
            }
        }).Select(task => task.Result).ToList();
    }

    public void ShutdownChannels()
    {
        foreach (var channel in _channels)
        {
            channel.ShutdownAsync().Wait();
        }
    }
}