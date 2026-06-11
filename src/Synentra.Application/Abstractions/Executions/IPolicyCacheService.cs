using Vectra.Domain.Policies;

namespace Vectra.Application.Abstractions.Executions;

public interface IPolicyCacheService
{
    Task<(IReadOnlyList<PolicyDefinition> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
