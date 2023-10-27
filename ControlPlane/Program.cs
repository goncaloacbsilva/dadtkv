using System.Diagnostics;
using CommandLine;

public enum ShowConsole
{
    CLIENT,
    TRANSACTION,
    LEASE,
    ALL
}

class Program
{
    private void launchClient(string[] args, ShowConsole showConsole)
    {
        Process process = new Process();
        ProcessStartInfo startInfo = new ProcessStartInfo();
        //file path to Client.exe
        startInfo.FileName = @"C:\Users\renat\Desktop\dadtkv\Client\app\Client.exe";
        startInfo.Arguments = string.Join(" ", args);
        if (showConsole == ShowConsole.CLIENT || showConsole == ShowConsole.ALL)
        {
            startInfo.CreateNoWindow = false;
            startInfo.WindowStyle = ProcessWindowStyle.Normal;
            startInfo.UseShellExecute = true;
            startInfo.RedirectStandardOutput = false;
            startInfo.RedirectStandardError = false;
        }
        process.StartInfo = startInfo;
        process.Start();
    }

    public void launchTransactionManager(string[] args, ShowConsole showConsole)
    {
        try
        {
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            //file path to TransactionManager.exe
            startInfo.FileName = @"C:\Users\renat\Desktop\dadtkv\TransactionManager\app\TransactionManager.exe";
            startInfo.Arguments = string.Join(" ", args);
            if (showConsole == ShowConsole.TRANSACTION || showConsole == ShowConsole.ALL) 
            { 
                startInfo.CreateNoWindow = false;
                startInfo.WindowStyle = ProcessWindowStyle.Normal;
                startInfo.UseShellExecute = true;
                startInfo.RedirectStandardOutput = false;
                startInfo.RedirectStandardError = false;
            }
            process.StartInfo = startInfo;
            process.Start();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public void launchLease(string[] args, ShowConsole showConsole)
    {
        Process process = new Process();
        ProcessStartInfo startInfo = new ProcessStartInfo();
        //file path to LeaseManager.exe
        startInfo.FileName = @"C:\Users\renat\Desktop\dadtkv\LeaseManager\app\LeaseManager.exe";
        startInfo.Arguments = string.Join(" ", args);
        if (showConsole == ShowConsole.LEASE || showConsole == ShowConsole.ALL)
        {
            startInfo.CreateNoWindow = false;
            startInfo.WindowStyle = ProcessWindowStyle.Normal;
            startInfo.UseShellExecute = true;
            startInfo.RedirectStandardOutput = false;
            startInfo.RedirectStandardError = false;
        }
        process.StartInfo = startInfo;
        process.Start();
    }

    private void fileReader(string filePath, ShowConsole showConsole, string logLevel)
    {
        // Read config
        var lines = File.ReadAllLines(filePath);

        foreach (var line in lines)
        {
            //interprets the lines with # as comments
            if (line[0] != '#')
            {
                var command = line.Split(" ");

                switch (command[0])
                {
                    //interprets the lines with P at the beginning as processes
                    case "P":
                        //sees if it is a client process
                        if (command[2] == "C")
                        {
                            string[] args = { filePath, command[1], command[3], logLevel};
                            Console.WriteLine("Launching client");
                            launchClient(args, showConsole);
                        }
                        //sees if it is a transaction manager process
                        else if (command[2] == "T")
                        {
                            Console.WriteLine("Launching TM");
                            var address = command[3].Split("//")[1].Split(":");

                            var args = (new[]
                            {
                                filePath, command[1],logLevel
                            }).Concat(address);

                            launchTransactionManager(args.ToArray(), showConsole);
                        }
                        //sees if it is a lease manager process
                        else if (command[2] == "L")
                        {
                            Console.WriteLine("Launching Lease");
                            var address = command[3].Split("//")[1].Split(":");

                            var args = (new[]
                            {
                                filePath, command[1],logLevel
                            }).Concat(address);
                            launchLease(args.ToArray(), showConsole); 
                        }
                    break;
                }
            }
        }
    }
    
    public class Options
    {
        [Value(0)]
        public string FilePath { get; set; }

        [Option("view", Required = false, Default = "ALL", HelpText = "Define which terminals shold open:\n" +
            "ALL           : Show the terminals of all the clients, transaction managers and lease managers\n" +
            "CLIENT        : Only show the terminals of the clients\n" +
            "TRANSACTION   : Only show the terminals of the transaction managers\n" +
            "LEASE         : Only show the terminals of the lease managers\n")]
        public string View { get; set; }

        [Option("logLevel", Required = false, Default = "ALL", HelpText = "Define which log level to see:\n" +
            "DEBUG           : Show the detailed information of the program and processes\n" +
            "INFORMATION     : Show the relevant informatINFO of the program and processes\n")]
        public string LogLevel { get; set; }
    }

    public static void Main(string[] args)
    {

        Parser.Default.ParseArguments<Options>(args)
        .WithParsed<Options>(o =>
        {
            ShowConsole consoleMode = ShowConsole.ALL;
            
            Program pm = new Program();
            
            switch (o.View.ToLower()) {
                case "transaction":
                    consoleMode = ShowConsole.TRANSACTION;
                    break;
                case "lease":
                    consoleMode = ShowConsole.LEASE;
                    break;
                case "client":
                    consoleMode = ShowConsole.CLIENT;
                    break;
                default:
                    consoleMode = ShowConsole.ALL; 
                    break;
            }

            pm.fileReader(o.FilePath,consoleMode, o.LogLevel);
            
            Console.WriteLine("Press Enter to shutdown");
            while (Console.ReadKey(true).Key != ConsoleKey.Enter)
            {
                Console.WriteLine("Press Enter to shutdown");
            }
        });


        
        //change file path
        //string filePath = "C:\\Users\\renat\\Desktop\\dadtkv\\ControlPlane\\configs\\sample.txt";
        
    }
}
