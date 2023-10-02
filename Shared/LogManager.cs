using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Shared;

public class LogManager
{
    private Logger _logger;

    public LogManager(string identifier, LogEventLevel level)
    {
        _logger = new LoggerConfiguration().Enrich.WithProperty("Identifier", identifier).MinimumLevel.Is(level).WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} [{Level}] ({Identifier}) {Message}{NewLine}{Exception}").CreateLogger();
    }

    public Logger Logger => _logger;
}