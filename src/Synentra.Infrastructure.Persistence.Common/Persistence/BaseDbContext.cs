using Microsoft.EntityFrameworkCore;
using Synentra.Domain.Agents;
using Synentra.Domain.AuditTrails;
using Synentra.Infrastructure.Persistence.Common.Exceptions;

namespace Synentra.Infrastructure.Persistence.Common;

public abstract class BaseDbContext : DbContext, IDatabaseContext
{
    protected BaseDbContext(
            DbContextOptions options): base(options)
    {
    }

    public DbSet<AuditTrail> AuditLogs { get; set; }
    public DbSet<Agent> Agents { get; set; }
    public DbSet<AgentHistory> AgentHistories { get; set; }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new DatabaseSaveException(ex);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        try
        {
            base.OnModelCreating(modelBuilder);
        }
        catch (Exception ex)
        {
            throw new DatabaseModelCreatingException(ex);
        }
    }
}