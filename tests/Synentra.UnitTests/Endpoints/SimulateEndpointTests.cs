using Microsoft.AspNetCore.Http;
using Vectra.Application.Abstractions.Dispatchers;
using Vectra.Application.Features.Simulations.SimulateDecision;
using Vectra.BuildingBlocks.Errors;
using Vectra.BuildingBlocks.Results;
using Vectra.Endpoints;

namespace Vectra.UnitTests.Endpoints;

public class SimulateEndpointTests
{
    private readonly IDispatcher _dispatcher;

    public SimulateEndpointTests()
    {
        _dispatcher = Substitute.For<IDispatcher>();
    }

    [Fact]
    public async Task Run_MissingAgentId_Returns401()
    {
        var context = new DefaultHttpContext();
        var request = new SimulateDecisionRequest(
            Method: "GET",
            Path: "/api/data",
            TargetUrl: null,
            PolicyName: null,
            Headers: null,
            ContentType: null,
            Body: null);

        var result = await Simulate.Run(request, context, _dispatcher, CancellationToken.None);

        AssertStatusCode(result, 401);
    }

    [Fact]
    public async Task Run_DispatcherForbidden_Returns403()
    {
        _dispatcher.Dispatch(Arg.Any<SimulateDecisionRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<SimulateDecisionResult>.FailureAsync(
                Error.Forbidden(new ErrorCode(0700001, ErrorCategory.Security), "forbidden")));

        var context = new DefaultHttpContext();
        context.Items["AgentId"] = Guid.NewGuid();

        var request = new SimulateDecisionRequest(
            Method: "GET",
            Path: "/api/data",
            TargetUrl: null,
            PolicyName: null,
            Headers: null,
            ContentType: null,
            Body: null);

        var result = await Simulate.Run(request, context, _dispatcher, CancellationToken.None);

        AssertStatusCode(result, 403);
    }

    [Fact]
    public async Task Run_Success_Returns200()
    {
        _dispatcher.Dispatch(Arg.Any<SimulateDecisionRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<SimulateDecisionResult>.SuccessAsync(
                new SimulateDecisionResult(Vectra.Domain.Policies.DecisionType.Allow, null, 0.2, "default")));

        var context = new DefaultHttpContext();
        context.Items["AgentId"] = Guid.NewGuid();

        var request = new SimulateDecisionRequest(
            Method: "POST",
            Path: "/api/data",
            TargetUrl: null,
            PolicyName: null,
            Headers: new Dictionary<string, string>(),
            ContentType: "application/json",
            Body: "{\"x\":1}");

        var result = await Simulate.Run(request, context, _dispatcher, CancellationToken.None);

        AssertStatusCode(result, 200);
    }

    private static void AssertStatusCode(IResult httpResult, int expected)
        => HttpTestHelpers.ExecuteAndGetStatusCode(httpResult).Should().Be(expected);
}
