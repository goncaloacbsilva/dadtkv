// See https://aka.ms/new-console-template for more information

using System;
using System.Diagnostics;
using System.IO;
using Shared;

class Program
{
    private void launchClient(string[] args)
    {
        Process process = new Process();
        ProcessStartInfo startInfo = new ProcessStartInfo();
        //change file path
        startInfo.FileName = @"C:\Users\renat\Desktop\dadtkv\Client\app\Client.exe";
        startInfo.Arguments = string.Join(" ", args);
        startInfo.CreateNoWindow = false;
        startInfo.WindowStyle = ProcessWindowStyle.Normal;
        startInfo.UseShellExecute = true;
        startInfo.RedirectStandardOutput = false;
        startInfo.RedirectStandardError = false;
        process.StartInfo = startInfo;
        process.Start();
    }

    public void launchTransactionManager(string[] args)
    {
        try
        {
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            //change file path
            startInfo.FileName = @"C:\Users\renat\Desktop\dadtkv\TransactionManager\app\TransactionManager.exe";
            startInfo.Arguments = string.Join(" ", args);
            startInfo.CreateNoWindow = false;
            startInfo.WindowStyle = ProcessWindowStyle.Normal;
            startInfo.UseShellExecute = true;
            startInfo.RedirectStandardOutput = false;
            startInfo.RedirectStandardError = false;
            process.StartInfo = startInfo;
            process.Start();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public void launchLease(string[] args)
    {
        Process process = new Process();
        ProcessStartInfo startInfo = new ProcessStartInfo();
        //change file path
        startInfo.FileName = @"C:\Users\renat\Desktop\dadtkv\LeaseManager\app\LeaseManager.exe";
        startInfo.Arguments = string.Join(" ", args);
        startInfo.CreateNoWindow = false;
        startInfo.WindowStyle = ProcessWindowStyle.Normal;
        startInfo.UseShellExecute = true;
        startInfo.RedirectStandardOutput = false;
        startInfo.RedirectStandardError = false;
        process.StartInfo = startInfo;
        process.Start();
    }

    private void fileReader(string filePath)
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
                            launchClient(args);
                        }
                        else if (command[2] == "T")
                        {
                            Console.WriteLine("Launching TM");
                            var address = command[3].Split("//")[1].Split(":");

                            var args = (new[]
                            {
                                filePath, command[1]
                            }).Concat(address);

                            launchTransactionManager(args.ToArray());
                        }
                        else if (command[2] == "L")
                        {
                            Console.WriteLine("Launching Lease");
                            var address = command[3].Split("//")[1].Split(":");

                            var args = (new[]
                            {
                                filePath, command[1]
                            }).Concat(address);

                            launchLease(args.ToArray()); 
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
        pm.fileReader(filePath);
    }
}
