using Client;
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

        //change log level information to see more or less information
        //debug to see all information or information to only see relevat information
        var logManager = new LogManager(identifier, LogEventLevel.Debug);
        ConfigurationManager configurationManager = new ConfigurationManager(configPath, identifier, false, logManager);
        ConnectionManager connectionManager = new ConnectionManager(configurationManager.TransactionManagers(), logManager);
        ScriptParser parser = new ScriptParser(scriptPath, connectionManager, identifier, logManager);
        
        configurationManager.WaitForTestStart();
        
        parser.Run();

    }
}