using FluentAssertions;
using NSubstitute;
using Synentra.Application.Abstractions.Persistence;
using Synentra.Application.Features.Audit.AuditList;
using Synentra.Domain.AuditTrails;

namespace Synentra.Application.UnitTests.Features.Audit;

public class AuditListHandlerTests
{
    private readonly IAuditRepository _auditRepository = Substitute.For<IAuditRepository>();
    private readonly AuditListHandler _sut;

    public AuditListHandlerTests()
    {
        _sut = new AuditListHandler(_auditRepository);
    }

    [Fact]
    public async Task Handle_ShouldReturnPaginatedSuccess_WithMappedAudits()
    {
        var trails = new List<AuditTrail>
        {
            new() { Id = 1, Action = "GET /x", TargetUrl = "/x", Status = "Allow" },
            new() { Id = 2, Action = "POST /y", TargetUrl = "/y", Status = "Deny" }
        };
        _auditRepository.GetPagedAsync(1, 25, CancellationToken.None)
            .Returns((trails.AsReadOnly() as IReadOnlyList<AuditTrail>, 2));

        var result = await _sut.Handle(new AuditListRequest(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenRepositoryIsNull()
    {
        var act = () => new AuditListHandler(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("auditRepository");
    }
}
