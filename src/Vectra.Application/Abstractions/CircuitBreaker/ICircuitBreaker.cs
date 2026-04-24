namespace Vectra.Application.Abstractions.CircuitBreaker;

public interface ICircuitBreaker
{
    /// <summary>
    /// Returns true if the upstream host circuit is closed (requests allowed).
    /// </summary>
    bool IsAllowed(string host);

    /// <summary>
    /// Records a successful upstream call.
    /// </summary>
    void RecordSuccess(string host);

    /// <summary>
    /// Records a failed upstream call.
    /// </summary>
    void RecordFailure(string host);
}
