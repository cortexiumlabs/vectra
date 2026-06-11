using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Synentra.Application.Abstractions.Persistence;
using Synentra.BuildingBlocks.Configuration.System;
using Synentra.Infrastructure.Persistence.Common;
using Synentra.Infrastructure.Persistence.Sqlite.Contexts;
using Synentra.Infrastructure.Persistence.Sqlite.Repositories;
using Synentra.Infrastructure.Persistence.Sqlite.Services;

namespace Synentra.Infrastructure.Persistence.Sqlite;

public static class DependencyInjection
{
    public static IServiceCollection AddSqlitePersistenceLayer(
        this IServiceCollection services)
    {
        services
            .AddScoped<IAgentRepository, AgentRepository>()
            .AddScoped<IAgentHistoryRepository, AgentHistoryRepository>()
            .AddScoped<IAuditRepository, AuditRepository>()
            .AddScoped<IDatabaseInitializer, SqliteDatabaseInitializer>();

        services.AddPooledDbContextFactory<SqliteApplicationContext>((sp, options) =>
        {
            var db = sp.GetRequiredService<IOptions<SystemConfiguration>>()
                       .Value.Storage.Database;

            options.UseSqlite(db.Providers.Sqlite.ConnectionString);
        });

        return services;
    }
}