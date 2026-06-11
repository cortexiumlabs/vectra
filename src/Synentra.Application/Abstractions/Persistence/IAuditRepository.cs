using Vectra.Domain.AuditTrails;

namespace Vectra.Application.Abstractions.Persistence;

public interface IAuditRepository
{
    Task AddAsync(AuditTrail auditTrail, CancellationToken cancellationToken = default);
}