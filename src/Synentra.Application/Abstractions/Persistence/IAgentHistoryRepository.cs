using Synentra.Application.Models;
using Synentra.Domain.Agents;

namespace Synentra.Application.Abstractions.Persistence;

public interface IAgentHistoryRepository
{
    Task<AgentStats?> GetStatsAsync(Guid agentId, TimeSpan window, CancellationToken cancellationToken = default);
    Task<AgentBaseline?> GetBaselineAsync(Guid agentId, TimeSpan lookback, CancellationToken cancellationToken = default);
    Task<AgentHistory?> GetRecentAsync(Guid agentId, TimeSpan window, CancellationToken cancellationToken = default);
    Task RecordRequestAsync(Guid agentId, bool wasViolation, double riskScore, CancellationToken cancellationToken = default);
}