using Microsoft.EntityFrameworkCore;
using Vectra.Domain.Agents;
using Vectra.Domain.AuditTrails;

namespace Vectra.Infrastructure.Persistence.Common;

public interface IDatabaseContext
{
    public DbSet<AuditTrail> AuditLogs { get; set; }
    public DbSet<Agent> Agents { get; set; }
    public DbSet<AgentHistory> AgentHistories { get; set; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}