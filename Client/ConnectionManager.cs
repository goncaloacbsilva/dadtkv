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
    private LogManager _logManager;

    public ConnectionManager(List<ServerEntry> servers, LogManager logManager)
    {
        _servers = servers;
        _nextTM = 0;
        _attempts = 0;

        // Client will iterate the servers list at least 2 times before giving up
        _maxAttempts = servers.Count * 2;
        _logManager = logManager;
    }

    public int GetTMIndex() {

        int hash = 0;

        foreach (char c in _logManager.Identifier) {
            hash += (int)c;
        }

        int hashCode = Math.Abs(hash);

        return (hashCode % (_servers.Count + 1));
    } 
    
    private void GetNewConnection(bool init)
    {
        if (_channel != null)
        {
            
            _logManager.Logger.Debug("[Connection Manager]: Closing channel...");
            // Shutdown before connect to a new channel
            _channel.ShutdownAsync();
        }

        if (init)
        {
            _nextTM = GetTMIndex();
        }

        var address = _servers[_nextTM];

        try
        {
            _logManager.Logger.Debug("[Connection Manager]: Attempting to connect to {0}:{1}", address.host, address.port);
            _channel = new Channel(address.host, address.port, ChannelCredentials.Insecure);
            _client = new TransactionManagerService.TransactionManagerServiceClient(_channel);
        }
        catch (Exception e)
        {
            _logManager.Logger.Error("[Connection Manager]: Error: {0}", e.Message);
        }

        _nextTM++;

        if (_nextTM == _servers.Count)
        {
            _nextTM = 0;
        }
    }

    public async Task HandleRPCCall<T>(Func<Task<T>> rpcAction)
    {
        bool gotResponse = false;
        
        if (_client == null || _channel == null)
        {
            GetNewConnection(true);
        }

        _attempts = 0;

        do
        {
            try
            {
                _attempts++;
                
                _logManager.Logger.Information("[RPCCall]: Sending request... (Attempt {0})", _attempts);
                var response = await rpcAction();

                _logManager.Logger.Information("[RPCCall Response]: {@0}", response);
                
                gotResponse = true;
                _attempts = 0;
            }
            catch (Exception e)
            {
                _logManager.Logger.Error("[RPCCall Error]: {0}", e.Message);

                if (_attempts >= _maxAttempts)
                {
                    _channel.ShutdownAsync().Wait();
                    Environment.Exit(1);
                }
                
                GetNewConnection(false);
            }
        } while (!gotResponse);
    }

    public TransactionManagerService.TransactionManagerServiceClient Client => _client;
}