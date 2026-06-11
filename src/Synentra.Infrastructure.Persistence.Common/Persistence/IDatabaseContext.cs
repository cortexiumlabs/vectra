using Microsoft.EntityFrameworkCore;
using Synentra.Domain.Agents;
using Synentra.Domain.AuditTrails;

namespace Synentra.Infrastructure.Persistence.Common;

public interface IDatabaseContext
{
    public DbSet<AuditTrail> AuditLogs { get; set; }
    public DbSet<Agent> Agents { get; set; }
    public DbSet<AgentHistory> AgentHistories { get; set; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}