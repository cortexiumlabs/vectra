using Vectra.Domain.Agents;

namespace Vectra.Application.Abstractions.Persistence;

public interface IAgentRepository
{
    Task<Agent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Agent agent, CancellationToken cancellationToken = default);
    Task UpdateAsync(Agent agent, CancellationToken cancellationToken = default);
}
