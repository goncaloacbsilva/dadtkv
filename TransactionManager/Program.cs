// See https://aka.ms/new-console-template for more information

using Grpc.Core;
using Grpc.Core.Interceptors;
using Serilog;
using Serilog.Events;
using Shared;
using TransactionManager;

public class Program
{
    static void Main(string[] args)
    {

        if (args.Length < 3)
        {
            Console.WriteLine("ERROR: Arguments required");
            return;
        }
        
        // ./TransactionManager <configPath> <identifier> <address> <port>
        
        string configPath = args[0];
        string identifier = args[1];
        string address = args[2];
        int port = int.Parse(args[3]);

        var logManager = new LogManager(identifier, LogEventLevel.Debug);
        var configurationManager = new ConfigurationManager(configPath, identifier, logManager);
        
        var server = new Server
        {
            Services = { TransactionManagerService.BindService(new TransactionService(configurationManager, logManager)).Intercept(new ServerExceptionsInterceptor(logManager.Logger)) },
            Ports = { new ServerPort(address, port, ServerCredentials.Insecure) }
        };
        
        
        server.Start();
        logManager.Logger.Information($"Server listening at port {port}. Press any key to terminate");
        Console.ReadKey();
    }   
}