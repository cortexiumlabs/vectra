using Vectra.Domain.Policies;

namespace Vectra.Application.Abstractions.Persistence;

public interface IPolicyRepository
{
    Task<PolicyDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PolicyDefinition?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(PolicyDefinition policy, CancellationToken cancellationToken = default);
    Task UpdateAsync(PolicyDefinition policy, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}