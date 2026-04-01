using Vectra.Core.Entities;

namespace Vectra.Core.Interfaces;

public interface IAuditRepository
{
    Task AddAsync(AuditLog log, CancellationToken cancellationToken = default);
}