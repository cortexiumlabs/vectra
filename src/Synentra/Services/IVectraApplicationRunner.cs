namespace Vectra.Services;

public interface IVectraApplicationRunner
{
    Task RunAsync(string[] args, CancellationToken cancellationToken);
}