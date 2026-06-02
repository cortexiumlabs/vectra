using Vectra.Domain.Agents;

namespace Vectra.Application.Abstractions.Security;

public interface IAgentRequestAccessService
{
    Task<AgentRequestAccessResult> GetAgentAsync(
        Guid agentId, 
        CancellationToken cancellationToken = default);
}

public readonly record struct AgentRequestAccessResult(
    bool IsAllowed,
    Agent? Agent,
    string? ForbiddenReason);