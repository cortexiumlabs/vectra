using Microsoft.EntityFrameworkCore;
using Vectra.Core.Entities;

namespace Vectra.Infrastructure.Persistence.Abstractions;

public interface IDatabaseContext
{
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<Agent> Agents { get; set; }
    public DbSet<Policy> Policies { get; set; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}