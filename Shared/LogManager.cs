using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Shared;

public class LogManager
{
    private Logger _logger;
    private string _identifier;

    public LogManager(string identifier, LogEventLevel level)
    {
        _logger = new LoggerConfiguration().Enrich.WithProperty("Identifier", identifier).MinimumLevel.Is(level).WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} [{Level}] ({Identifier}) {Message}{NewLine}{Exception}").CreateLogger();
        _identifier = identifier;
    }

    public Logger Logger => _logger;
    public string Identifier => _identifier;
}