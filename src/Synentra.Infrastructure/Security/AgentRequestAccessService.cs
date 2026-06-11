using Microsoft.Extensions.Options;
using Vectra.Application.Abstractions.Persistence;
using Vectra.Application.Abstractions.Security;
using Vectra.BuildingBlocks.Configuration.Security;
using Vectra.Domain.Agents;

namespace Vectra.Infrastructure.Security;

public sealed class AgentRequestAccessService : IAgentRequestAccessService
{
    private readonly IAgentRepository _agentRepository;
    private readonly SecurityConfiguration _security;

    public AgentRequestAccessService(
        IAgentRepository agentRepository,
        IOptions<SecurityConfiguration> security)
    {
        _agentRepository = agentRepository ?? throw new ArgumentNullException(nameof(agentRepository));
        _security = security?.Value ?? throw new ArgumentNullException(nameof(security));
    }

    public async Task<AgentRequestAccessResult> GetAgentAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        var agent = await _agentRepository.GetByIdAsync(agentId, cancellationToken);
        if (agent == null)
            return new AgentRequestAccessResult(false, null, "Agent is not active");

        if (agent.Status == AgentStatus.Quarantined)
            return new AgentRequestAccessResult(false, agent, "Agent is quarantined");

        if (agent.Status != AgentStatus.Active)
            return new AgentRequestAccessResult(false, agent, "Agent is not active");

        if (_security.AgentQuarantine.Enabled != false && agent.TrustScore < _security.AgentQuarantine.TrustScoreFloor)
        {
            agent.Quarantine();
            await _agentRepository.UpdateAsync(agent, cancellationToken);
            return new AgentRequestAccessResult(false, agent, "Agent is quarantined");
        }

        return new AgentRequestAccessResult(true, agent, null);
    }
}
