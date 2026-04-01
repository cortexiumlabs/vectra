using Microsoft.EntityFrameworkCore;
using Vectra.Core.Entities;
using Vectra.Core.Interfaces;
using Vectra.Infrastructure.Persistence.Sqlite.Contexts;

namespace Vectra.Infrastructure.Persistence.Sqlite.Repositories;

public class PolicyRepository : IPolicyRepository
{
    private readonly IDbContextFactory<SqliteApplicationContext> _appContextFactory;

    public PolicyRepository(IDbContextFactory<SqliteApplicationContext> appContextFactory)
    {
        _appContextFactory = appContextFactory ?? throw new ArgumentNullException(nameof(appContextFactory));
    }

    public async Task<List<Policy>> GetForAgentAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        await using var context = await _appContextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Policies.Where(p => p.AgentId == agentId).ToListAsync(cancellationToken);
    }
}