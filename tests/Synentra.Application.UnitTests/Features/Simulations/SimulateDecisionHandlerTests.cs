using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Synentra.Application.Abstractions.Executions;
using Synentra.Application.Abstractions.Security;
using Synentra.Application.Features.Simulations.SimulateDecision;
using Synentra.Domain.Agents;
using Synentra.Domain.Policies;
using Synentra.BuildingBlocks.Results;

namespace Synentra.Application.UnitTests.Features.Simulations;

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

        _decisionEngine.SimulateAsync(Arg.Any<string>(), Arg.Any<Synentra.Application.Models.RequestContext>(), Arg.Any<CancellationToken>())
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

    [Fact]
    public async Task Handle_IncludesSelectedHeadersAndBody_AndTruncates()
    {
        var agentId = Guid.NewGuid();
        var agent = new Agent("tester", "owner", "hash") { PolicyName = "p" };
        agent.UpdateTrustScore(0.5);

        _accessService.GetAgentAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new AgentRequestAccessResult(true, agent, null));

        string? capturedSemantic = null;
        var longBody = new string('a', 2000);

        _decisionEngine.SimulateAsync(Arg.Do<string>(s => capturedSemantic = s), Arg.Any<Synentra.Application.Models.RequestContext>(), Arg.Any<CancellationToken>())
            .Returns(DecisionResult.Allow(0.1));

        var request = new SimulateDecisionRequest(
            Method: "POST",
            Path: "/very/long/path",
            TargetUrl: null,
            PolicyName: null,
            Headers: new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json",
                ["User-Agent"] = "unit-test-agent"
            },
            ContentType: null,
            Body: longBody)
        {
            AgentId = agentId
        };

        var result = await _sut.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        capturedSemantic.Should().NotBeNull();
        capturedSemantic!.Length.Should().BeLessThanOrEqualTo(512);
        capturedSemantic.Should().Contain("POST /very/long/path");
        capturedSemantic.Should().Contain("Content-Type: application/json");
        capturedSemantic.Should().Contain("User-Agent: unit-test-agent");
        capturedSemantic.Should().Contain("Body:");
    }

    [Fact]
    public async Task Handle_NoBody_DoesNotIncludeBodyText()
    {
        var agentId = Guid.NewGuid();
        var agent = new Agent("a", "o", "h") { PolicyName = "pp" };
        agent.UpdateTrustScore(0.2);

        _accessService.GetAgentAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new AgentRequestAccessResult(true, agent, null));

        string? capturedSemantic = null;
        _decisionEngine.SimulateAsync(Arg.Do<string>(s => capturedSemantic = s), Arg.Any<Synentra.Application.Models.RequestContext>(), Arg.Any<CancellationToken>())
            .Returns(DecisionResult.Deny("reason", 0.0));

        var request = new SimulateDecisionRequest(
            Method: "GET",
            Path: "/health",
            TargetUrl: null,
            PolicyName: null,
            Headers: new Dictionary<string, string>
            {
                ["Content-Type"] = "text/plain"
            },
            ContentType: null,
            Body: null)
        {
            AgentId = agentId
        };

        var result = await _sut.Handle(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        capturedSemantic.Should().NotBeNull();
        capturedSemantic!.Should().Contain("GET /health");
        capturedSemantic.Should().Contain("Content-Type: text/plain");
        capturedSemantic.Should().NotContain("Body:");
    }
}
