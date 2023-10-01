// See https://aka.ms/new-console-template for more information

using Client;
using Grpc.Core;
using Shared;

public class Program
{
    public static void Main(string[] args)
    {
        string scriptPath = args[0];
        string configPath = args[1];

        ConfigurationManager configurationManager = new ConfigurationManager(configPath);
        ConnectionManager connectionManager = new ConnectionManager(configurationManager.TransactionManagers());
        ScriptParser parser = new ScriptParser(scriptPath, connectionManager);
        
        parser.Run();

    }
}