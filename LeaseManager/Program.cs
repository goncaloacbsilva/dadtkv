// See https://aka.ms/new-console-template for more information

using Grpc.Core;
using LeaseManager;

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
        
        string address = args[0];
        int port = int.Parse(args[1]);
        
        var server = new Server
        {
            Services = { LeaseManagerService.BindService(new LeaseService()) },
            Ports = { new ServerPort(address, port, ServerCredentials.Insecure) }
        };
        server.Start();
        Console.WriteLine($"Server listening at port {port}. Press any key to terminate");
        Console.ReadKey();
    }   
}