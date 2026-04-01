using Vectra.Core.Entities;

namespace Vectra.Core.Interfaces;

public interface IPolicyRepository
{
    Task<List<Policy>> GetForAgentAsync(Guid agentId, CancellationToken cancellationToken = default);
}