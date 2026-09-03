using Synentra.Domain.AuditTrails;

namespace Synentra.Application.Abstractions.Persistence;

public interface IAuditRepository
{
    Task<(IReadOnlyList<AuditTrail> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AuditTrail?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task AddAsync(AuditTrail auditTrail, CancellationToken cancellationToken = default);
}