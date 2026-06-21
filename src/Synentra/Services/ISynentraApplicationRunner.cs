namespace Synentra.Services;

public interface ISynentraApplicationRunner
{
    Task RunAsync(string[] args, CancellationToken cancellationToken);
}