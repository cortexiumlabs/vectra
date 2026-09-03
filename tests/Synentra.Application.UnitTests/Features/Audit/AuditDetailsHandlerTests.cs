using FluentAssertions;
using NSubstitute;
using Synentra.Application.Abstractions.Persistence;
using Synentra.Application.Errors;
using Synentra.Application.Features.Audit.AuditDetails;
using Synentra.Domain.AuditTrails;

namespace Synentra.Application.UnitTests.Features.Audit;

public class AuditDetailsHandlerTests
{
    private readonly IAuditRepository _auditRepository = Substitute.For<IAuditRepository>();
    private readonly AuditDetailsHandler _sut;

    public AuditDetailsHandlerTests()
    {
        _sut = new AuditDetailsHandler(_auditRepository);
    }

    [Fact]
    public async Task Handle_ShouldReturnDetails_WhenAuditExists()
    {
        _auditRepository.GetByIdAsync(5, CancellationToken.None)
            .Returns(new AuditTrail { Id = 5, Action = "GET /resource", TargetUrl = "/resource", Status = "Allow" });

        var result = await _sut.Handle(new AuditDetailsRequest { Id = 5 }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(5);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenAuditMissing()
    {
        _auditRepository.GetByIdAsync(404, CancellationToken.None).Returns((AuditTrail?)null);

        var result = await _sut.Handle(new AuditDetailsRequest { Id = 404 }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.ErrorCode.Should().Be(ApplicationErrorCodes.AuditTrailNotFound);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenRepositoryIsNull()
    {
        var act = () => new AuditDetailsHandler(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("auditRepository");
    }
}
