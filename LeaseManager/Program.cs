using Grpc.Core;
using LeaseManager;
using Serilog.Events;
using Shared;

public class Program
{
    static void Main(string[] args)
    {

        if (args.Length < 5)
        {
            Console.WriteLine("ERROR: Arguments required");
            return;
        }

        // ./LeaseManager <configPath> <identifier> <address> <port> <log level>

        string configPath = args[0];
        string identifier = args[1];
        string address = args[3];
        int port = int.Parse(args[4]);

        LogEventLevel logLevel = LogEventLevel.Information;

        switch (args[2]) {
            case "DEBUG":
                logLevel = LogEventLevel.Debug;
                break;
            default:
                logLevel = LogEventLevel.Information;
                break;
        }

        //change log level information to see more or less information
        //debug to see all information or information to only see relevat information
        var logManager = new LogManager(identifier, logLevel);

        var configurationManager = new ConfigurationManager(configPath, identifier, true, logManager);

        var server = new Server
        {
            Services = { LeaseManagerService.BindService(new LeaseService(configurationManager, logManager)) },
            Ports = { new ServerPort(address, port, ServerCredentials.Insecure) }
        };

        configurationManager.WaitForTestStart();

        server.Start();
        logManager.Logger.Debug($"Server listening at port {port}. Press any key to terminate");
        Console.ReadKey();
    }   
}