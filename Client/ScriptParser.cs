using System.Text.RegularExpressions;

namespace Client;

public class ScriptParser
{
    private TransactionManagerService.TransactionManagerServiceClient _client;
    private readonly string _path;

    public ScriptParser(TransactionManagerService.TransactionManagerServiceClient client, string path)
    {
        _client = client;
        _path = path;
    }

    private TxSubmitRequest ParseTx(string line)
    {
        TxSubmitRequest request = new TxSubmitRequest();
        
        var command = line.Split(" ");
        
        // Remove the parentheses and split the string by comma
        var parts = command[1].Trim('(', ')').Split(',');
        
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
                    var response = _client.TxSubmit(request);
                    Console.WriteLine(response);
                    break;
                case 'W':
                    var interval = int.Parse(line.Split(" ")[1]);
                    Console.WriteLine("[Script]: Wait {0}", interval);
                    System.Threading.Thread.Sleep(interval);
                    break;
            }
        }
    }
    
}