// See https://aka.ms/new-console-template for more information

using Grpc.Core;
using TransactionManager;

public class Program
{
    public string nome;
    static void Main(string[] args)
    {
        Program pm = new Program();
        if (args.Length < 1)
        {
            Console.WriteLine("ERROR: Arguments required");
            return;
        }
        
        string address = args[0];
        int port = int.Parse(args[1]);
        pm.nome = args[2];

        var server = new Server
        {
            Services = { TransactionManagerService.BindService(new TransactionService()) },
            Ports = { new ServerPort(address, port, ServerCredentials.Insecure) }
        };
        server.Start();
        Console.WriteLine($"Server listening at port {port}. Press any key to terminate");
        Console.ReadKey();
    }   
}