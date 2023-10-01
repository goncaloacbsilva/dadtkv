// See https://aka.ms/new-console-template for more information

using Client;
using Grpc.Core;
using Shared;

public class Program
{
    public static void Main(string[] args)
    {
        
        // ./Client <configPath> <identifier> <scriptPath>
        
        string configPath = args[0];
        string identifier = args[1];
        string scriptPath = args[2];

        ConfigurationManager configurationManager = new ConfigurationManager(configPath);
        ConnectionManager connectionManager = new ConnectionManager(configurationManager.TransactionManagers());
        ScriptParser parser = new ScriptParser(scriptPath, connectionManager);
        
        parser.Run();

    }
}