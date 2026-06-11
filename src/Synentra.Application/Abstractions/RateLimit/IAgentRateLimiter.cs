namespace Vectra.Application.Abstractions.RateLimit;

public interface IAgentRateLimiter
{
    /// <summary>
    /// Returns true if the request is allowed; false if the agent has exceeded its rate limit.
    /// </summary>
    Task<bool> IsAllowedAsync(Guid agentId, CancellationToken cancellationToken = default);
}
