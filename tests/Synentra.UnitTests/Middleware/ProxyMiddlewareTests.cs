using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Net;
using Synentra.Application.Abstractions.CircuitBreaker;
using Synentra.Application.Abstractions.Executions;
using Synentra.Application.Abstractions.Persistence;
using Synentra.Application.Abstractions.RateLimit;
using Synentra.Application.Abstractions.Security;
using Synentra.Application.Models;
using Synentra.BuildingBlocks.Configuration.Security;
using Synentra.BuildingBlocks.Configuration.Security.AgentAuth;
using Synentra.Domain.Agents;
using Synentra.Domain.Policies;
using Synentra.Middleware;
using Synentra.Services;

namespace Synentra.UnitTests.Middleware;

public class ProxyMiddlewareTests
{
    private readonly ILogger<ProxyMiddleware> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public ProxyMiddlewareTests()
    {
        _logger = Substitute.For<ILogger<ProxyMiddleware>>();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
    }

    [Fact]
    public void Constructor_NullNext_Throws()
    {
        var act = () => new ProxyMiddleware(null!, _httpClientFactory);
        act.Should().Throw<ArgumentNullException>().WithParameterName("next");
    }

    [Fact]
    public void Constructor_NullHttpClientFactory_Throws()
    {
        var act = () => new ProxyMiddleware(_ => Task.CompletedTask, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("httpClientFactory");
    }

    [Fact]
    public async Task InvokeAsync_PathNotStartingWithProxy_Returns400()
    {
        var middleware = BuildMiddleware(_ => Task.CompletedTask);
        var context = BuildContext("/not-proxy/something");
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task InvokeAsync_InvalidTargetUrl_Returns400()
    {
        var middleware = BuildMiddleware(_ => Task.CompletedTask);
        var context = BuildContext("/proxy/not-a-url");
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task InvokeAsync_MissingAgentId_Returns401()
    {
        var decisionEngine = Substitute.For<IDecisionEngine>();
        var hitlService = Substitute.For<IHitlService>();
        var rateLimiter = Substitute.For<IAgentRateLimiter>();
        var circuitBreaker = Substitute.For<ICircuitBreaker>();

        var middleware = BuildMiddleware(_ => Task.CompletedTask);
        var context = BuildContext("/proxy/http://example.com/api",
            decisionEngine, hitlService, rateLimiter: rateLimiter, circuitBreaker: circuitBreaker);
        context.Response.Body = new MemoryStream();
        // No AgentId in context.Items

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task InvokeAsync_AgentNotFound_Returns403()
    {
        var agentId = Guid.NewGuid();
        var accessService = Substitute.For<IAgentRequestAccessService>();
        accessService.GetAgentAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new AgentRequestAccessResult(false, null, "Agent is not active"));

        var middleware = BuildMiddleware(_ => Task.CompletedTask);
        var context = BuildContext("/proxy/http://example.com/api",
            accessService: accessService);
        context.Items["AgentId"] = agentId;
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task InvokeAsync_QuarantinedAgent_Returns403()
    {
        var agentId = Guid.NewGuid();
        var agent = new Agent("test", "owner", "hash");
        agent.Quarantine();

        var accessService = Substitute.For<IAgentRequestAccessService>();
        accessService.GetAgentAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new AgentRequestAccessResult(false, agent, "Agent is quarantined"));

        var middleware = BuildMiddleware(_ => Task.CompletedTask);
        var context = BuildContext("/proxy/http://example.com/api",
            accessService: accessService);
        context.Items["AgentId"] = agentId;
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task InvokeAsync_TrustScoreBelowFloor_AutoQuarantinesAndReturns403()
    {
        var agentId = Guid.NewGuid();
        var agent = new Agent("test", "owner", "hash");
        agent.UpdateTrustScore(0.1);

        var accessService = Substitute.For<IAgentRequestAccessService>();
        accessService.GetAgentAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new AgentRequestAccessResult(false, agent, "Agent is quarantined"));

        var middleware = BuildMiddleware(_ => Task.CompletedTask);
        var context = BuildContext("/proxy/http://example.com/api",
            accessService: accessService);
        context.Items["AgentId"] = agentId;
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task InvokeAsync_RevokedAgent_Returns403()
    {
        var agentId = Guid.NewGuid();
        var accessService = Substitute.For<IAgentRequestAccessService>();
        accessService.GetAgentAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new AgentRequestAccessResult(false, null, "Agent is not active"));

        var middleware = BuildMiddleware(_ => Task.CompletedTask);
        var context = BuildContext("/proxy/http://example.com/api",
            accessService: accessService);
        context.Items["AgentId"] = agentId;
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task InvokeAsync_RateLimitExceeded_Returns429()
    {
        var agentId = Guid.NewGuid();
        var accessService = Substitute.For<IAgentRequestAccessService>();
        accessService.GetAgentAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new AgentRequestAccessResult(true, new Agent("test", "owner", "hash"), null));

        var rateLimiter = Substitute.For<IAgentRateLimiter>();
        rateLimiter.IsAllowedAsync(agentId, Arg.Any<CancellationToken>()).Returns(false);

        var middleware = BuildMiddleware(_ => Task.CompletedTask);
        var context = BuildContext("/proxy/http://example.com/api",
            accessService: accessService, rateLimiter: rateLimiter);
        context.Items["AgentId"] = agentId;
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(429);
        context.Response.Headers["Retry-After"].ToString().Should().Be("60");
    }

    [Fact]
    public async Task InvokeAsync_CircuitBreakerOpen_Returns503()
    {
        var agentId = Guid.NewGuid();
        var accessService = Substitute.For<IAgentRequestAccessService>();
        accessService.GetAgentAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new AgentRequestAccessResult(true, new Agent("test", "owner", "hash"), null));

        var rateLimiter = Substitute.For<IAgentRateLimiter>();
        rateLimiter.IsAllowedAsync(agentId, Arg.Any<CancellationToken>()).Returns(true);

        var circuitBreaker = Substitute.For<ICircuitBreaker>();
        circuitBreaker.IsAllowed("example.com").Returns(false);

        var middleware = BuildMiddleware(_ => Task.CompletedTask);
        var context = BuildContext("/proxy/http://example.com/api",
            accessService: accessService, rateLimiter: rateLimiter, circuitBreaker: circuitBreaker);
        context.Items["AgentId"] = agentId;
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task InvokeAsync_DecisionDenied_Returns403()
    {
        var agentId = Guid.NewGuid();
        var accessService = Substitute.For<IAgentRequestAccessService>();
        accessService.GetAgentAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new AgentRequestAccessResult(true, new Agent("test", "owner", "hash"), null));

        var rateLimiter = Substitute.For<IAgentRateLimiter>();
        rateLimiter.IsAllowedAsync(agentId, Arg.Any<CancellationToken>()).Returns(true);

        var circuitBreaker = Substitute.For<ICircuitBreaker>();
        circuitBreaker.IsAllowed(Arg.Any<string>()).Returns(true);

        var decisionEngine = Substitute.For<IDecisionEngine>();
        decisionEngine.EvaluateAsync(Arg.Any<string>(), Arg.Any<RequestContext>(), Arg.Any<CancellationToken>())
            .Returns(DecisionResult.Deny("policy violation"));

        var middleware = BuildMiddleware(_ => Task.CompletedTask);
        var context = BuildContext("/proxy/http://example.com/api",
            decisionEngine: decisionEngine, accessService: accessService,
            rateLimiter: rateLimiter, circuitBreaker: circuitBreaker);
        context.Items["AgentId"] = agentId;
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task InvokeAsync_DecisionHitl_Returns202()
    {
        var agentId = Guid.NewGuid();
        var accessService = Substitute.For<IAgentRequestAccessService>();
        accessService.GetAgentAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new AgentRequestAccessResult(true, new Agent("test", "owner", "hash"), null));

        var rateLimiter = Substitute.For<IAgentRateLimiter>();
        rateLimiter.IsAllowedAsync(agentId, Arg.Any<CancellationToken>()).Returns(true);

        var circuitBreaker = Substitute.For<ICircuitBreaker>();
        circuitBreaker.IsAllowed(Arg.Any<string>()).Returns(true);

        var decisionEngine = Substitute.For<IDecisionEngine>();
        decisionEngine.EvaluateAsync(Arg.Any<string>(), Arg.Any<RequestContext>(), Arg.Any<CancellationToken>())
            .Returns(DecisionResult.Hitl("requires review"));

        var hitlService = Substitute.For<IHitlService>();
        hitlService.SuspendRequestAsync(Arg.Any<RequestContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("hitl-123");

        var middleware = BuildMiddleware(_ => Task.CompletedTask);
        var context = BuildContext("/proxy/http://example.com/api",
            decisionEngine: decisionEngine, hitlService: hitlService,
            accessService: accessService, rateLimiter: rateLimiter, circuitBreaker: circuitBreaker);
        context.Items["AgentId"] = agentId;
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(202);
        context.Response.Headers.Location.ToString().Should().Contain("hitl-123");
    }

    [Fact]
    public async Task InvokeAsync_UpstreamHttpError_Records503AndCircuitFailure()
    {
        var agentId = Guid.NewGuid();
        var accessService = Substitute.For<IAgentRequestAccessService>();
        accessService.GetAgentAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new AgentRequestAccessResult(true, new Agent("test", "owner", "hash"), null));

        var rateLimiter = Substitute.For<IAgentRateLimiter>();
        rateLimiter.IsAllowedAsync(agentId, Arg.Any<CancellationToken>()).Returns(true);

        var circuitBreaker = Substitute.For<ICircuitBreaker>();
        circuitBreaker.IsAllowed(Arg.Any<string>()).Returns(true);

        var decisionEngine = Substitute.For<IDecisionEngine>();
        decisionEngine.EvaluateAsync(Arg.Any<string>(), Arg.Any<RequestContext>(), Arg.Any<CancellationToken>())
            .Returns(DecisionResult.Allow());

        // Fake HttpClient that throws
        var handler = new FailingHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        _httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var middleware = BuildMiddleware(_ => Task.CompletedTask);
        var context = BuildContext("/proxy/http://example.com/api",
            decisionEngine: decisionEngine, accessService: accessService,
            rateLimiter: rateLimiter, circuitBreaker: circuitBreaker);
        context.Items["AgentId"] = agentId;
        context.Request.Body = new MemoryStream();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(503);
        circuitBreaker.Received().RecordFailure("example.com");
    }

    [Fact]
    public async Task InvokeAsync_ForwardsAuthorizationHeader_ButStripsSynentraHeader()
    {
        var agentId = Guid.NewGuid();
        var accessService = Substitute.For<IAgentRequestAccessService>();
        accessService.GetAgentAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new AgentRequestAccessResult(true, new Agent("test", "owner", "hash"), null));

        var rateLimiter = Substitute.For<IAgentRateLimiter>();
        rateLimiter.IsAllowedAsync(agentId, Arg.Any<CancellationToken>()).Returns(true);

        var circuitBreaker = Substitute.For<ICircuitBreaker>();
        circuitBreaker.IsAllowed(Arg.Any<string>()).Returns(true);

        var decisionEngine = Substitute.For<IDecisionEngine>();
        decisionEngine.EvaluateAsync(Arg.Any<string>(), Arg.Any<RequestContext>(), Arg.Any<CancellationToken>())
            .Returns(DecisionResult.Allow());

        HttpRequestMessage? forwardedRequest = null;
        var handler = new CaptureRequestHttpMessageHandler(request => forwardedRequest = request, HttpStatusCode.OK, "ok");
        var httpClient = new HttpClient(handler);
        _httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var middleware = BuildMiddleware(_ => Task.CompletedTask);
        var context = BuildContext("/proxy/http://example.com/api",
            decisionEngine: decisionEngine, accessService: accessService,
            rateLimiter: rateLimiter, circuitBreaker: circuitBreaker);

        context.Items["AgentId"] = agentId;
        context.Request.Headers.Authorization = "Bearer upstream-token";
        context.Request.Headers["Synentra-Authorization"] = "Bearer gateway-token";
        context.Request.Body = new MemoryStream();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        forwardedRequest.Should().NotBeNull();
        forwardedRequest!.Headers.Authorization.Should().NotBeNull();
        forwardedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        forwardedRequest.Headers.Authorization.Parameter.Should().Be("upstream-token");
        forwardedRequest.Headers.Contains("Synentra-Authorization").Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_SuccessfulProxy_CopiesResponseStatusCode()
    {
        var agentId = Guid.NewGuid();
        var accessService = Substitute.For<IAgentRequestAccessService>();
        accessService.GetAgentAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new AgentRequestAccessResult(true, new Agent("test", "owner", "hash"), null));

        var rateLimiter = Substitute.For<IAgentRateLimiter>();
        rateLimiter.IsAllowedAsync(agentId, Arg.Any<CancellationToken>()).Returns(true);

        var circuitBreaker = Substitute.For<ICircuitBreaker>();
        circuitBreaker.IsAllowed(Arg.Any<string>()).Returns(true);

        var decisionEngine = Substitute.For<IDecisionEngine>();
        decisionEngine.EvaluateAsync(Arg.Any<string>(), Arg.Any<RequestContext>(), Arg.Any<CancellationToken>())
            .Returns(DecisionResult.Allow());

        var handler = new FixedResponseHttpMessageHandler(HttpStatusCode.OK, "upstream-body");
        var httpClient = new HttpClient(handler);
        _httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var middleware = BuildMiddleware(_ => Task.CompletedTask);
        var context = BuildContext("/proxy/http://example.com/api",
            decisionEngine: decisionEngine, accessService: accessService,
            rateLimiter: rateLimiter, circuitBreaker: circuitBreaker);
        context.Items["AgentId"] = agentId;
        context.Request.Body = new MemoryStream();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(200);
        circuitBreaker.Received().RecordSuccess("example.com");
    }

    [Fact]
    public async Task InvokeAsync_SetsObservabilityItems_WhenDecisionMade()
    {
        var agentId = Guid.NewGuid();
        var accessService = Substitute.For<IAgentRequestAccessService>();
        accessService.GetAgentAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new AgentRequestAccessResult(true, new Agent("test", "owner", "hash"), null));

        var rateLimiter = Substitute.For<IAgentRateLimiter>();
        rateLimiter.IsAllowedAsync(agentId, Arg.Any<CancellationToken>()).Returns(true);

        var circuitBreaker = Substitute.For<ICircuitBreaker>();
        circuitBreaker.IsAllowed(Arg.Any<string>()).Returns(true);

        var decisionEngine = Substitute.For<IDecisionEngine>();
        decisionEngine.EvaluateAsync(Arg.Any<string>(), Arg.Any<RequestContext>(), Arg.Any<CancellationToken>())
            .Returns(DecisionResult.Allow(0.73));

        var handler = new FixedResponseHttpMessageHandler(HttpStatusCode.OK, "upstream-body");
        var httpClient = new HttpClient(handler);
        _httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var middleware = BuildMiddleware(_ => Task.CompletedTask);
        var context = BuildContext("/proxy/http://example.com/api",
            decisionEngine: decisionEngine, accessService: accessService,
            rateLimiter: rateLimiter, circuitBreaker: circuitBreaker);
        context.Items["AgentId"] = agentId;
        context.Request.Body = new MemoryStream();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Items.Should().ContainKey("RiskScore");
        context.Items["RiskScore"].Should().Be(0.73);
        context.Items.Should().ContainKey("Decision");
        context.Items["Decision"].Should().Be("allow");
    }

    [Fact]
    public async Task InvokeAsync_Upstream500_RecordsCircuitFailure()
    {
        var agentId = Guid.NewGuid();
        var accessService = Substitute.For<IAgentRequestAccessService>();
        accessService.GetAgentAsync(agentId, Arg.Any<CancellationToken>())
            .Returns(new AgentRequestAccessResult(true, new Agent("test", "owner", "hash"), null));

        var rateLimiter = Substitute.For<IAgentRateLimiter>();
        rateLimiter.IsAllowedAsync(agentId, Arg.Any<CancellationToken>()).Returns(true);

        var circuitBreaker = Substitute.For<ICircuitBreaker>();
        circuitBreaker.IsAllowed(Arg.Any<string>()).Returns(true);

        var decisionEngine = Substitute.For<IDecisionEngine>();
        decisionEngine.EvaluateAsync(Arg.Any<string>(), Arg.Any<RequestContext>(), Arg.Any<CancellationToken>())
            .Returns(DecisionResult.Allow());

        var handler = new FixedResponseHttpMessageHandler(HttpStatusCode.InternalServerError, "error");
        var httpClient = new HttpClient(handler);
        _httpClientFactory.CreateClient(Arg.Any<string>()).Returns(httpClient);

        var middleware = BuildMiddleware(_ => Task.CompletedTask);
        var context = BuildContext("/proxy/http://example.com/api",
            decisionEngine: decisionEngine, accessService: accessService,
            rateLimiter: rateLimiter, circuitBreaker: circuitBreaker);
        context.Items["AgentId"] = agentId;
        context.Request.Body = new MemoryStream();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        circuitBreaker.Received().RecordFailure("example.com");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private ProxyMiddleware BuildMiddleware(RequestDelegate next)
        => new(next, _httpClientFactory);

    private static DefaultHttpContext BuildContext(
        string path,
        IDecisionEngine? decisionEngine = null,
        IHitlService? hitlService = null,
        IAgentRequestAccessService? accessService = null,
        IAgentRepository? agentRepository = null,
        IAgentRateLimiter? rateLimiter = null,
        ICircuitBreaker? circuitBreaker = null)
    {
        decisionEngine ??= Substitute.For<IDecisionEngine>();
        hitlService ??= Substitute.For<IHitlService>();
        accessService ??= Substitute.For<IAgentRequestAccessService>();
        agentRepository ??= Substitute.For<IAgentRepository>();
        rateLimiter ??= Substitute.For<IAgentRateLimiter>();
        circuitBreaker ??= Substitute.For<ICircuitBreaker>();

        var services = new ServiceCollection();
        services.AddSingleton(decisionEngine);
        services.AddSingleton(hitlService);
        services.AddSingleton(accessService);
        services.AddSingleton(agentRepository);
        services.AddSingleton(rateLimiter);
        services.AddSingleton(circuitBreaker);
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new SecurityConfiguration
        {
            AgentAuth = new AgentAuthConfiguration
            {
                CustomHeaderName = "Synentra-Authorization"
            }
        }));
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Path = path;
        context.Request.Method = "GET";
        context.Request.Body = Stream.Null;
        return context;
    }

    // ── Fake handlers ─────────────────────────────────────────────────────

    private sealed class FailingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("connection refused");
    }

    private sealed class FixedResponseHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _body;

        public FixedResponseHttpMessageHandler(HttpStatusCode statusCode, string body)
        {
            _statusCode = statusCode;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body)
            };
            return Task.FromResult(response);
        }
    }

    private sealed class CaptureRequestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Action<HttpRequestMessage> _onRequest;
        private readonly HttpStatusCode _statusCode;
        private readonly string _body;

        public CaptureRequestHttpMessageHandler(Action<HttpRequestMessage> onRequest, HttpStatusCode statusCode, string body)
        {
            _onRequest = onRequest;
            _statusCode = statusCode;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _onRequest(request);
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body)
            };
            return Task.FromResult(response);
        }
    }
}
