using Serilog;

namespace Synentra.Infrastructure.Logging;

public interface ILoggerFactory
{
    ILogger CreateLogger();
}