using System.Text.RegularExpressions;
using Grpc.Core;
using Shared;

namespace Client;

public class ScriptParser
{
    private readonly string _path;
    private string _identifier;
    private ConnectionManager _connectionManager;
    private LogManager _logManager;

    public ScriptParser(string path, ConnectionManager connectionManager, string identifier, LogManager logManager)
    {
        _path = path;
        _identifier = identifier;
        _connectionManager = connectionManager;
        _logManager = logManager;
    }

    private TxSubmitRequest ParseTx(string line)
    {
        TxSubmitRequest request = new TxSubmitRequest
        {
            ClientId = _identifier
        };
        
        var command = line.Split(" ");
        
        // Remove the parentheses and split the string by comma
        var parts = command[1].Trim('(', ')').Split(',');

        if (parts.Length == 1 && parts[0].Equals(""))
        {
            parts = Array.Empty<string>();
        }
        
        // Remove double quotes and trim whitespace for each value
        foreach (var part in parts)
        {
            string trimmedValue = part.Trim('"');
            request.ReadEntries.Add(trimmedValue);
        }
        
        // Define a regular expression pattern to match name-number pairs
        string pattern = @"<""([^\""]+)"",(\d+)>";
        
        // Use regex to find matches in the input string
        MatchCollection matches = Regex.Matches(command[2], pattern);

        // Extract and store the name-number pairs
        foreach (Match match in matches)
        {
            request.WriteEntries.Add(new DadInt
            {
                Key = match.Groups[1].Value,
                Value = int.Parse(match.Groups[2].Value)
            });
        }

        return request;
    }

    public void Run()
    {
        var lines = File.ReadAllLines(_path);
        
        foreach (var line in lines) {
            switch (line[0])
            {
                case '#':
                    break;
                case 'T':
                    // Transaction
                    var request = ParseTx(line);
                    _logManager.Logger.Debug("[Script]: {@0}", request);
                    
                    var response = _connectionManager.HandleRPCCall(() => _connectionManager.Client.TxSubmit(request));

                    _logManager.Logger.Debug("[Script]: {@0}", response);
                    break;
                case 'W':
                    var interval = int.Parse(line.Split(" ")[1]);
                    _logManager.Logger.Debug("[Script]: Wait {0}", interval);
                    System.Threading.Thread.Sleep(interval);
                    break;
            }
        }
    }
    
}