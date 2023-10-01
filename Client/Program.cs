// See https://aka.ms/new-console-template for more information

using Client;
using Grpc.Core;
using Shared;

public class Program
{
    public string name;
    public static void Main(string[] args)
    {
        Program pm = new Program();
        string scriptPath = args[0];
        string configPath = args[1];
        pm.name = args[2];

        ConfigurationManager configurationManager = new ConfigurationManager(configPath);
        ConnectionManager connectionManager = new ConnectionManager(configurationManager.TransactionManagers());
        ScriptParser parser = new ScriptParser(scriptPath, connectionManager);
        
        parser.Run();

    }
}