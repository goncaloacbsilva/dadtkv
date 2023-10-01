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
        startInfo.FileName = @"/home/goncalo/Documents/MEIC/DAD/dadtkv/Client/bin/Debug/net7.0/Client";
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
        startInfo.FileName = @"/home/goncalo/Documents/MEIC/DAD/dadtkv/TransactionManager/bin/Debug/net7.0/TransactionManager";
        startInfo.Arguments = string.Join(" ", args);
        startInfo.CreateNoWindow = false;
        try
        {
            Process.Start(startInfo);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public void launchTransactionManager(string[] args)
    {
        Thread thread = new Thread(() => launchTransactionManagerProcess(args));
        thread.Start();
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
                        else if (command[3] == "L")
                        {
                            //launch leases
                        }
                        break;
                }
            }
        }
    }

    public static void Main(string[] args)
    {
        Program pm = new Program();
        string filePath = "/home/goncalo/Documents/MEIC/DAD/dadtkv/ControlPlane/configs/sample.txt";
        Console.WriteLine("Introduce the path to the configuration file");
        //Console.ReadLine();
        pm.fileReader(filePath);
    }
}
