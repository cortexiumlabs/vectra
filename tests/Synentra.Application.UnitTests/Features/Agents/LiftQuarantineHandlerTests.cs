using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Synentra.Application.Abstractions.Persistence;
using Synentra.Application.Errors;
using Synentra.Application.Features.Agents.LiftQuarantine;
using Synentra.Domain.Agents;

namespace Synentra.Application.UnitTests.Features.Agents;

public class LiftQuarantineHandlerTests
{
    private readonly ILogger<LiftQuarantineHandler> _logger = Substitute.For<ILogger<LiftQuarantineHandler>>();
    private readonly IAgentRepository _agentRepository = Substitute.For<IAgentRepository>();

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act = () => new LiftQuarantineHandler(null!, _agentRepository);

        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_NullRepository_ThrowsArgumentNullException()
    {
        var act = () => new LiftQuarantineHandler(_logger, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("agentRepository");
    }

    [Fact]
    public async Task Handle_AgentNotFound_ReturnsFailure()
    {
        var request = new LiftQuarantineRequest { AgentId = Guid.NewGuid().ToString() };
        _agentRepository.GetByIdAsync(Arg.Any<Guid>(), CancellationToken.None).Returns((Agent?)null);
        var sut = new LiftQuarantineHandler(_logger, _agentRepository);

        var result = await sut.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.ErrorCode.Should().Be(ApplicationErrorCodes.AgentNotFound);
        await _agentRepository.DidNotReceive().UpdateAsync(Arg.Any<Agent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingQuarantinedAgent_LiftsQuarantineAndReturnsSuccess()
    {
        var agentId = Guid.NewGuid();
        var request = new LiftQuarantineRequest { AgentId = agentId.ToString() };
        var agent = new Agent("agent", "owner", "hash");
        agent.Quarantine();

        _agentRepository.GetByIdAsync(agentId, CancellationToken.None).Returns(agent);
        var sut = new LiftQuarantineHandler(_logger, _agentRepository);

        var result = await sut.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        agent.Status.Should().Be(AgentStatus.Active);
        await _agentRepository.Received(1).UpdateAsync(agent, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_InvalidAgentId_ThrowsFormatException()
    {
        var request = new LiftQuarantineRequest { AgentId = "invalid-guid" };
        var sut = new LiftQuarantineHandler(_logger, _agentRepository);

        var act = () => sut.Handle(request, CancellationToken.None);

        await act.Should().ThrowAsync<FormatException>();
    }
}