using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Vectra.Application.Abstractions.Executions;
using Vectra.Application.Abstractions.Security;
using Vectra.Application.Features.Simulations.SimulateDecision;
using Vectra.Domain.Agents;
using Vectra.Domain.Policies;
using Vectra.BuildingBlocks.Results;

namespace Vectra.Application.UnitTests.Features.Simulations;

public class SimulateDecisionHandlerTests
{
    private readonly IDecisionEngine _decisionEngine = Substitute.For<IDecisionEngine>();
    private readonly IAgentRequestAccessService _accessService = Substitute.For<IAgentRequestAccessService>();
    private readonly ILogger<SimulateDecisionHandler> _logger = Substitute.For<ILogger<SimulateDecisionHandler>>();

    private readonly SimulateDecisionHandler _sut;

    public SimulateDecisionHandlerTests()
    {
        _sut = new SimulateDecisionHandler(_decisionEngine, _accessService, _logger);
    }

    [Fact]
    public async Task Handle_MissingAgentId_ReturnsUnauthorized()
    {
        var request = new SimulateDecisionRequest(
            Method: "GET",
            Path: "/api/data",
            TargetUrl: null,
            PolicyName: null,
            Headers: null,
            ContentType: null,
            Body: null)
        {
            AgentId = null
        };

        var result = await _sut.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Handle_AgentNotAllowed_ReturnsForbidden()
    {
        var agentId = Guid.NewGuid();
        _accessService.GetAgentAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new AgentRequestAccessResult(false, null, "Agent is not active"));

        var request = new SimulateDecisionRequest(
            Method: "GET",
            Path: "/api/data",
            TargetUrl: null,
            PolicyName: null,
            Headers: null,
            ContentType: null,
            Body: null)
        {
            AgentId = agentId
        };

        var result = await _sut.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Handle_Allows_ReturnsDecision()
    {
        var agentId = Guid.NewGuid();
        var agent = new Agent("test", "owner", "hash") { PolicyName = "default-policy" };
        agent.UpdateTrustScore(0.77);

        _accessService.GetAgentAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new AgentRequestAccessResult(true, agent, null));

        _decisionEngine.SimulateAsync(Arg.Any<Vectra.Application.Models.RequestContext>(), Arg.Any<CancellationToken>())
            .Returns(DecisionResult.Allow(0.12));

        var request = new SimulateDecisionRequest(
            Method: "POST",
            Path: "/admin/export",
            TargetUrl: "https://service.local/admin/export",
            PolicyName: null,
            Headers: new Dictionary<string, string> { ["X-Test"] = "1" },
            ContentType: "application/json",
            Body: "intent text")
        {
            AgentId = agentId
        };

        var result = await _sut.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Type.Should().Be(DecisionType.Allow);
        result.Value.TrustScore.Should().Be(0.12);
        result.Value.PolicyName.Should().Be("default-policy");
    }

    [Fact]
    public void Constructor_NullDecisionEngine_Throws()
    {
        var act = () => new SimulateDecisionHandler(null!, _accessService, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("decisionEngine");
    }
}
