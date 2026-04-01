namespace Vectra.Core.Interfaces;

public interface IOpaClient
{
    Task<OpaDecision> EvaluateAsync(string package, object input, CancellationToken cancellationToken = default);
}

public record OpaDecision(string Decision);