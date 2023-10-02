// See https://aka.ms/new-console-template for more information

using Grpc.Core;
using LeaseManager;
using Serilog.Events;
using Shared;

public class Program
{
    static void Main(string[] args)
    {

        if (args.Length < 2)
        {
            Console.WriteLine("ERROR: Arguments required");
            return;
        }

        // ./LeaseManager <address> <port>

        string address = args[2];
        string identifier = args[1];
        int port = int.Parse(args[3]);

        var logManager = new LogManager(identifier, LogEventLevel.Debug);

        var server = new Server
        {
            Services = { LeaseManagerService.BindService(new LeaseService(logManager)) },
            Ports = { new ServerPort(address, port, ServerCredentials.Insecure) }
        };
        server.Start();
        logManager.Logger.Debug($"Server listening at port {port}. Press any key to terminate");
        Console.ReadKey();
    }   
}