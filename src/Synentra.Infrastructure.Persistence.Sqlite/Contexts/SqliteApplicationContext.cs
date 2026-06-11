using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vectra.Infrastructure.Persistence.Common;
using Vectra.Infrastructure.Persistence.Common.Exceptions;

namespace Vectra.Infrastructure.Persistence.Sqlite.Contexts;

public class SqliteApplicationContext : BaseDbContext
{
    private readonly ILogger<SqliteApplicationContext> _logger;

    public SqliteApplicationContext(
        DbContextOptions<SqliteApplicationContext> contextOptions,
        ILogger<SqliteApplicationContext> logger)
        : base(contextOptions, logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        try
        {
            base.OnModelCreating(modelBuilder);
            ApplyEntityConfigurations(modelBuilder);
        }
        catch (Exception ex)
        {
            throw new DatabaseModelCreatingException(ex);
        }
    }

    private void ApplyEntityConfigurations(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SqliteApplicationContext).Assembly);
    }
}