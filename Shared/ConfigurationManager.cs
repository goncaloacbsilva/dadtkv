﻿namespace Shared;

public enum ServerType
{
    Transaction,
    Lease
}

public enum ServerState
{
    Normal,
    Crashed
}

public struct ServerEntry
{
    public string identifier;
    public ServerType type;
    public string host;
    public int port;
}

public struct SuspectPair
{
    public string suspecting;
    public string suspected;
}

public struct TimeSlotState
{
    public int timeSlot;
    public Dictionary<string, ServerState> states;
    public List<SuspectPair> suspects;
}

public class ConfigurationManager
{
    private List<ServerEntry> _servers;
    private List<TimeSlotState> _states;
    private int _timeSlots;
    private int _slotDuration;
    private DateTime _startTime;
    private string _identifier;
    
    
    public ConfigurationManager(string configPath, string identifier)
    {
        Console.WriteLine("[Config Manager]: Reading path: {0}", configPath);
        
        // Read config
        var lines = File.ReadAllLines(configPath);
        
        _servers = new List<ServerEntry>();
        _states = new List<TimeSlotState>();
        _identifier = identifier;
        
        foreach (var line in lines) {
            if (line[0] != '#')
            {
                var command = line.Split(" ");
                
                switch (command[0])
                {
                    case "P":
                        // Ignore client processes
                        if (command[2] != "C")
                        {
                            ServerType type = (command[2] == "T") ? ServerType.Transaction : ServerType.Lease;
                            var address = command[3].Replace("http://", "").Split(":");
                            _servers.Add(new ServerEntry
                            {
                                identifier = command[1],
                                type = type,
                                host = address[0],
                                port = int.Parse(address[1])
                            });
                        }
                        break;
                    case "S":
                        _timeSlots = int.Parse(command[1]);
                        break;
                    case "T":
                        if (!DateTime.TryParse(command[1], out _startTime))
                        {
                            Console.WriteLine("[{0}][Config Manager]: Error: Failed to parse start time", _identifier);
                        }
                        break;
                    case "D":
                        _slotDuration = int.Parse(command[1]);
                        break;
                    case "F":
                        int timeSlot = int.Parse(command[1]);
                        var states = new Dictionary<string, ServerState>();
                        var suspects = new List<SuspectPair>();
                        
                        // Parse server states
                        for (int i = 0; i < _servers.Count; i++)
                        {
                            ServerState state = (command[2 + i] == "N") ? ServerState.Normal : ServerState.Crashed;
                            states.Add(_servers[i].identifier, state);
                        }

                        // Parse suspecting processes
                        for (int i = 2 + _servers.Count; i < command.Length; i++)
                        {
                            var pair = command[i].Trim('(', ')').Split(',');
                            suspects.Add(new SuspectPair
                            {
                                suspecting = pair[0],
                                suspected = pair[1]
                            });
                        }
                        
                        _states.Add(new TimeSlotState
                        {
                            timeSlot = timeSlot,
                            states = states,
                            suspects = suspects
                        });
                        break;
                }
            }
        }
        
        Console.WriteLine("[{0}][Config Manager]: Start Time: {1}", _identifier, _startTime);
        Console.WriteLine("[{0}][Config Manager]: {1} time slots with {2} milliseconds each", _identifier, _timeSlots, _slotDuration);
        Console.WriteLine("[{0}][Config Manager]: Parsed {1} servers and {2} states", _identifier, _servers.Count, _states.Count);
    }

    public List<ServerEntry> Servers => _servers;

    public List<ServerEntry> TransactionManagers()
    {
        return _servers.Where(server => server.type == ServerType.Transaction).ToList();
    }
    
    public List<TimeSlotState> States => _states;

    public int TimeSlots => _timeSlots;

    public int SlotDuration => _slotDuration;

    public DateTime StartTime => _startTime;

    public string Identifier => _identifier;
}