// See https://aka.ms/new-console-template for more information

using Client;
using Grpc.Core;
using Serilog.Core;
using Serilog.Events;
using Shared;

public class Program
{
    public static void Main(string[] args)
    {
        
        // ./Client <configPath> <identifier> <scriptPath>
        
        string configPath = args[0];
        string identifier = args[1];
        string scriptPath = args[2];

        var logManager = new LogManager(identifier, LogEventLevel.Debug);

        ConfigurationManager configurationManager = new ConfigurationManager(configPath, identifier, logManager);
        ConnectionManager connectionManager = new ConnectionManager(configurationManager.TransactionManagers(),logManager);
        ScriptParser parser = new ScriptParser(scriptPath, connectionManager, identifier, logManager);
        
        parser.Run();

    }
}