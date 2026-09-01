using Microsoft.EntityFrameworkCore;
using Synentra.Infrastructure.Persistence.Common;
using Synentra.Infrastructure.Persistence.Common.Exceptions;

namespace Synentra.Infrastructure.Persistence.Sqlite.Contexts;

public class SqliteApplicationContext : BaseDbContext
{
    public SqliteApplicationContext(
        DbContextOptions<SqliteApplicationContext> contextOptions)
        : base(contextOptions)
    {
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

    private static void ApplyEntityConfigurations(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SqliteApplicationContext).Assembly);
    }
}