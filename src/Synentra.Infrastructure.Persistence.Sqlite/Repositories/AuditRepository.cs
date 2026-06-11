using Microsoft.EntityFrameworkCore;
using Synentra.Application.Abstractions.Persistence;
using Synentra.Domain.AuditTrails;
using Synentra.Infrastructure.Persistence.Sqlite.Contexts;

namespace Synentra.Infrastructure.Persistence.Sqlite.Repositories;

public class AuditRepository : IAuditRepository
{
    private readonly IDbContextFactory<SqliteApplicationContext> _appContextFactory;

    public AuditRepository(IDbContextFactory<SqliteApplicationContext> appContextFactory)
    {
        _appContextFactory = appContextFactory ?? throw new ArgumentNullException(nameof(appContextFactory));
    }

    public async Task AddAsync(AuditTrail auditTrail, CancellationToken cancellationToken = default)
    {
        await using var context = await _appContextFactory.CreateDbContextAsync(cancellationToken);
        await context.AuditLogs.AddAsync(auditTrail, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}