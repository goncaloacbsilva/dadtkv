using Grpc.Core;
using Shared;

namespace Client;



public class ConnectionManager
{
    private List<ServerEntry> _servers;
    private TransactionManagerService.TransactionManagerServiceClient _client;
    private Channel _channel;
    private int _nextTM;
    private int _attempts;
    private int _maxAttempts;

    public ConnectionManager(List<ServerEntry> servers)
    {
        _servers = servers;
        _nextTM = 0;
        _attempts = 0;

        // Client will iterate the servers list at least 2 times before giving up
        _maxAttempts = servers.Count * 2;
    }
    
    private void GetNewConnection()
    {
        if (_channel != null)
        {
            // Shutdown before connect to a new channel
            _channel.ShutdownAsync().Wait();
        }

        var address = _servers[_nextTM];

        try
        {
            Console.WriteLine("[Connection Manager]: Attempting to connect to {0}:{1}", address.host, address.port);
            _channel = new Channel(address.host, address.port, ChannelCredentials.Insecure);
            _client = new TransactionManagerService.TransactionManagerServiceClient(_channel);
        }
        catch (Exception e)
        {
            Console.WriteLine("[Connection Manager]: Error: {0}", e.Message);
        }

        _nextTM++;

        if (_nextTM == _servers.Count)
        {
            _nextTM = 0;
        }
    }

    public T HandleRPCCall<T>(Func<T> rpcAction)
    {
        bool gotResponse = false;
        
        if (_client == null || _channel == null)
        {
            GetNewConnection();
        }

        do
        {
            try
            {
                _attempts++;
                
                Console.WriteLine("[RPCCall]: Sending request... (Attempt {0})", _attempts);
                var response = rpcAction();
                
                gotResponse = true;
                _attempts = 0;

                return response;
            }
            catch (Exception e)
            {
                Console.WriteLine("[RPCCall Error]: {0}", e);

                if (_attempts >= _maxAttempts)
                {
                    _channel.ShutdownAsync().Wait();
                    Environment.Exit(1);
                }
                
                GetNewConnection();
            }
        } while (!gotResponse);

        return default(T);
    }

    public TransactionManagerService.TransactionManagerServiceClient Client => _client;
}