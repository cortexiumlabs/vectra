using Serilog;

namespace Vectra.Infrastructure.Logging;

public interface ILoggerFactory
{
    ILogger CreateLogger();
}