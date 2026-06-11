using Synentra.Domain.AuditTrails;

namespace Synentra.Application.Abstractions.Persistence;

public interface IAuditRepository
{
    Task AddAsync(AuditTrail auditTrail, CancellationToken cancellationToken = default);
}