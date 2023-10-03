// See https://aka.ms/new-console-template for more information

using System;
using System.Diagnostics;
using System.IO;
using Shared;

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
        //change file path
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
            //change file path
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
        //change file path
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

    private void fileReader(string filePath, ShowConsole showConsole)
    {
        // Read config
        var lines = File.ReadAllLines(filePath);

        foreach (var line in lines)
        {
            if (line[0] != '#')
            {
                var command = line.Split(" ");

                switch (command[0])
                {
                    case "P":
                        if (command[2] == "C")
                        {
                            string[] args = { filePath, command[1], command[3] };
                            Console.WriteLine("Launching client");
                            launchClient(args, showConsole);
                        }
                        else if (command[2] == "T")
                        {
                            Console.WriteLine("Launching TM");
                            var address = command[3].Split("//")[1].Split(":");

                            var args = (new[]
                            {
                                filePath, command[1]
                            }).Concat(address);

                            launchTransactionManager(args.ToArray(), showConsole);
                        }
                        else if (command[2] == "L")
                        {
                            Console.WriteLine("Launching Lease");
                            var address = command[3].Split("//")[1].Split(":");

                            var args = (new[]
                            {
                                filePath, command[1]
                            }).Concat(address);
                            launchLease(args.ToArray(), showConsole); 
                        }
                    break;
                }
            }
        }
    }

    public static void Main(string[] args)
    {
        Program pm = new Program();
        //change file path
        string filePath = "C:\\Users\\renat\\Desktop\\dadtkv\\ControlPlane\\configs\\sample.txt";
        Console.WriteLine("Introduce the path to the configuration file");
        //Console.ReadLine();
        pm.fileReader(filePath,ShowConsole.LEASE);
        Console.WriteLine("Press Enter to shutdown");
        while (Console.ReadKey(true).Key != ConsoleKey.Enter)
        {
            Console.WriteLine("Press Enter to shutdown");
        }
    }
}
