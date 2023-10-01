// See https://aka.ms/new-console-template for more information

using System;
using System.Diagnostics;
using System.IO;

class Program
{
    private void launchClientProcess(string[] args)
    {
        Process process = new Process();
        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = @"C:\Users\renat\Desktop\dadtkv\Client\app\Client.exe";
        startInfo.Arguments = string.Join(" ", args);
        Process.Start(startInfo);
    }

    public void launchClient(string[] args)
    {
        Thread thread = new Thread(() => launchClientProcess(args));
        thread.Start();
    }

    public void launchTransactionManagerProcess(string[] args)
    {
        Process process = new Process();
        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = @"C:\Users\renat\Desktop\dadtkv\TransactionManager\app\TransactionManager.exe";
        startInfo.Arguments = string.Join(" ", args);
        startInfo.CreateNoWindow = false;
        Process.Start(startInfo);
    }

    public void launchTransactionManager(string[] args)
    {
        Thread thread = new Thread(() => launchTransactionManagerProcess(args));
        thread.Start();
    }

    private string fileReader(string filePath)
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
                        // Ignore client processes
                        if (command[2] == "C")
                        {
                            string[] args = { command[3], filePath, command[1] };

                            launchClient(args);
                        }
                        else if (command[2] == "T")
                        {
                            string[] args = command[3].Split("//")[1].Split(":");
                            args = args.Concat(new string[] { command[1] }).ToArray();
                            launchTransactionManager(args);
                        }
                        else if (command[3] == "L")
                        {
                            //launch leases
                        }
                        break;
                }
            }
        }

        return null;
    }

    static void Main(string[] args)
    {
        Program pm = new Program();
        string filePath = "C:\\Users\\renat\\Desktop\\dadtkv\\ControlPlane\\configs\\sample.txt";
        Console.WriteLine("Introduce the path to the configuration file");
        //Console.ReadLine();
        string s = pm.fileReader(filePath);
    }
}
