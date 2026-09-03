using Microsoft.EntityFrameworkCore;
using Synentra.Domain.AuditTrails;
using Synentra.Infrastructure.Persistence.Sqlite.Contexts;
using Synentra.Infrastructure.Persistence.Sqlite.Repositories;
using Synentra.Infrastructure.Persistence.Sqlite.UnitTests.Helpers;

namespace Synentra.Infrastructure.Persistence.Sqlite.UnitTests.Repositories;

public class AuditRepositoryTests
{
    private static IDbContextFactory<SqliteApplicationContext> CreateFactory(string dbName)
        => SqliteTestContextFactory.CreateFactory(dbName);

    [Fact]
    public void Constructor_NullFactory_ThrowsArgumentNullException()
    {
        Action act = () => new AuditRepository(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("appContextFactory");
    }

    [Fact]
    public async Task AddAsync_ValidAuditTrail_PersistsSuccessfully()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = CreateFactory(dbName);
        var repo = new AuditRepository(factory);

        var auditTrail = new AuditTrail
        {
            AgentId = Guid.NewGuid(),
            Action = "POST /api/users",
            TargetUrl = "https://api.example.com/users",
            Status = "ALLOWED",
            RiskScore = 0.1,
            Timestamp = DateTime.UtcNow
        };

        Func<Task> act = () => repo.AddAsync(auditTrail);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AddAsync_PersistsAuditTrailWithCorrectValues()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = CreateFactory(dbName);
        var repo = new AuditRepository(factory);
        var agentId = Guid.NewGuid();

        var auditTrail = new AuditTrail
        {
            AgentId = agentId,
            Action = "DELETE /api/resource",
            TargetUrl = "https://api.example.com/resource",
            Status = "DENIED",
            RiskScore = 0.95,
            Intent = "Suspicious",
            Reason = "High risk score",
            Timestamp = DateTime.UtcNow
        };

        await repo.AddAsync(auditTrail);

        // Verify via context directly
        await using var ctx = SqliteTestContextFactory.Create(dbName);
        var saved = await ctx.AuditLogs.FirstOrDefaultAsync(a => a.AgentId == agentId);

        saved.Should().NotBeNull();
        saved!.Action.Should().Be("DELETE /api/resource");
        saved.Status.Should().Be("DENIED");
        saved.RiskScore.Should().BeApproximately(0.95, 0.001);
    }

    [Fact]
    public async Task AddAsync_WithCancellationToken_PropagatesToken()
    {
        var factory = CreateFactory(Guid.NewGuid().ToString());
        var repo = new AuditRepository(factory);
        using var cts = new CancellationTokenSource();

        var auditTrail = new AuditTrail
        {
            AgentId = Guid.NewGuid(),
            Action = "GET /api/data",
            TargetUrl = "https://api.example.com/data",
            Status = "ALLOWED"
        };

        Func<Task> act = () => repo.AddAsync(auditTrail, cts.Token);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsItemsAndTotalCount()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = CreateFactory(dbName);
        var repo = new AuditRepository(factory);

        await repo.AddAsync(new AuditTrail
        {
            AgentId = Guid.NewGuid(),
            Action = "GET /a",
            TargetUrl = "/a",
            Status = "ALLOWED",
            Timestamp = DateTime.UtcNow.AddMinutes(-1)
        });

        await repo.AddAsync(new AuditTrail
        {
            AgentId = Guid.NewGuid(),
            Action = "GET /b",
            TargetUrl = "/b",
            Status = "DENIED",
            Timestamp = DateTime.UtcNow
        });

        var (items, totalCount) = await repo.GetPagedAsync(1, 10);

        totalCount.Should().Be(2);
        items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsAuditTrail_WhenExists()
    {
        var dbName = Guid.NewGuid().ToString();
        var factory = CreateFactory(dbName);
        var repo = new AuditRepository(factory);

        var auditTrail = new AuditTrail
        {
            AgentId = Guid.NewGuid(),
            Action = "PUT /resource",
            TargetUrl = "/resource",
            Status = "ALLOWED",
            Timestamp = DateTime.UtcNow
        };

        await repo.AddAsync(auditTrail);

        await using var ctx = SqliteTestContextFactory.Create(dbName);
        var id = await ctx.AuditLogs.Select(a => a.Id).FirstAsync();

        var result = await repo.GetByIdAsync(id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
    }
}
