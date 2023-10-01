// See https://aka.ms/new-console-template for more information

using Grpc.Core;
using TransactionManager;

public class Program
{
    static void Main(string[] args)
    {

        if (args.Length < 1)
        {
            Console.WriteLine("ERROR: Arguments required");
            return;
        }
        
        int port = int.Parse(args[0]);
        var server = new Server
        {
            Services = { TransactionManagerService.BindService(new TransactionService()) },
            Ports = { new ServerPort("localhost", port, ServerCredentials.Insecure) }
        };
        server.Start();
        Console.WriteLine($"Server listening at port {port}. Press any key to terminate");
        Console.ReadKey();
    }   
}