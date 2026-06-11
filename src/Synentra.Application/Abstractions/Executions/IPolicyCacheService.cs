using Synentra.Domain.Policies;

namespace Synentra.Application.Abstractions.Executions;

public interface IPolicyCacheService
{
    Task<(IReadOnlyList<PolicyDefinition> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
