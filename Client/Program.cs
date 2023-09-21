// See https://aka.ms/new-console-template for more information

using Client;
using Grpc.Core;

public class Program
{
    public static void Main(string[] args)
    {
        string path = args[0];

        var channel = new Channel("0.0.0.0", 3001, ChannelCredentials.Insecure);
        var client = new TransactionManagerService.TransactionManagerServiceClient(channel);

        ScriptParser parser = new ScriptParser(client, path);
        
        parser.Run();

        Console.WriteLine("[Script]: Shutting down channel...");
        channel.ShutdownAsync().Wait();

    }
}