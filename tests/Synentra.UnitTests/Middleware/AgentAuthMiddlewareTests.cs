using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Synentra.Application.Abstractions.Security;
using Synentra.BuildingBlocks.Configuration.Security;
using Synentra.BuildingBlocks.Configuration.Security.AgentAuth;

namespace Synentra.UnitTests.Middleware;

public class AgentAuthMiddlewareTests
{
    private readonly ILogger<Synentra.Middleware.AgentAuthMiddleware> _logger;
    private readonly IAgentAuthenticator _authenticator;

    public AgentAuthMiddlewareTests()
    {
        _logger = Substitute.For<ILogger<Synentra.Middleware.AgentAuthMiddleware>>();
        _authenticator = Substitute.For<IAgentAuthenticator>();
    }

    private HttpContext BuildContext(
        string? customAuthHeader = null,
        string? authorizationHeader = null,
        bool fallbackToAuthorization = false,
        bool useCustomHeader = true,
        string customHeaderName = "Synentra-Authorization")
    {
        var services = new ServiceCollection();
        services.AddSingleton(_authenticator);
        services.AddSingleton<IOptions<SecurityConfiguration>>(Options.Create(new SecurityConfiguration
        {
            AgentAuth = new AgentAuthConfiguration
            {
                UseCustomHeader = useCustomHeader,
                CustomHeaderName = customHeaderName,
                FallbackToAuthorization = fallbackToAuthorization
            }
        }));
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext
        {
            RequestServices = provider
        };

        if (customAuthHeader is not null)
            context.Request.Headers[customHeaderName] = customAuthHeader;

        if (authorizationHeader is not null)
            context.Request.Headers.Authorization = authorizationHeader;

        return context;
    }

    [Fact]
    public async Task InvokeAsync_NoAuthHeader_CallsNextWithoutSettingAgentId()
    {
        var next = Substitute.For<RequestDelegate>();
        next(Arg.Any<HttpContext>()).Returns(Task.CompletedTask);
        var middleware = new Synentra.Middleware.AgentAuthMiddleware(next, _logger);

        var context = BuildContext();
        await middleware.InvokeAsync(context);

        context.Items.ContainsKey("AgentId").Should().BeFalse();
        await next.Received(1).Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_EmptyAuthHeader_CallsNextWithoutSettingAgentId()
    {
        var next = Substitute.For<RequestDelegate>();
        next(Arg.Any<HttpContext>()).Returns(Task.CompletedTask);
        var middleware = new Synentra.Middleware.AgentAuthMiddleware(next, _logger);

        var context = BuildContext(customAuthHeader: "   ");
        await middleware.InvokeAsync(context);

        context.Items.ContainsKey("AgentId").Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_InvalidAuthScheme_DoesNotSetAgentId()
    {
        _authenticator.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ClaimsPrincipal?)null);

        var next = Substitute.For<RequestDelegate>();
        next(Arg.Any<HttpContext>()).Returns(Task.CompletedTask);
        var middleware = new Synentra.Middleware.AgentAuthMiddleware(next, _logger);

        var context = BuildContext(customAuthHeader: "Basic sometoken");
        await middleware.InvokeAsync(context);

        context.Items.ContainsKey("AgentId").Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_ValidBearerToken_SetsAgentId()
    {
        var agentId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, agentId.ToString())
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        _authenticator.ValidateAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(principal);

        var next = Substitute.For<RequestDelegate>();
        next(Arg.Any<HttpContext>()).Returns(Task.CompletedTask);
        var middleware = new Synentra.Middleware.AgentAuthMiddleware(next, _logger);

        var context = BuildContext(customAuthHeader: "Bearer valid-token");
        await middleware.InvokeAsync(context);

        context.Items["AgentId"].Should().Be(agentId);
    }

    [Fact]
    public async Task InvokeAsync_ValidBearerToken_WithTrustScore_SetsTrustScore()
    {
        var agentId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, agentId.ToString()),
            new Claim("trust_score", "0.85")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        _authenticator.ValidateAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(principal);

        var next = Substitute.For<RequestDelegate>();
        next(Arg.Any<HttpContext>()).Returns(Task.CompletedTask);
        var middleware = new Synentra.Middleware.AgentAuthMiddleware(next, _logger);

        var context = BuildContext(customAuthHeader: "Bearer valid-token");
        await middleware.InvokeAsync(context);

        context.Items["TrustScore"].Should().Be(0.85);
    }

    [Fact]
    public async Task InvokeAsync_ValidatorReturnsNull_DoesNotSetAgentId()
    {
        _authenticator.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ClaimsPrincipal?)null);

        var next = Substitute.For<RequestDelegate>();
        next(Arg.Any<HttpContext>()).Returns(Task.CompletedTask);
        var middleware = new Synentra.Middleware.AgentAuthMiddleware(next, _logger);

        var context = BuildContext(customAuthHeader: "Bearer invalid-token");
        await middleware.InvokeAsync(context);

        context.Items.ContainsKey("AgentId").Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_BearerWithNonGuidSub_DoesNotSetAgentId()
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "not-a-guid") };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        _authenticator.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(principal);

        var next = Substitute.For<RequestDelegate>();
        next(Arg.Any<HttpContext>()).Returns(Task.CompletedTask);
        var middleware = new Synentra.Middleware.AgentAuthMiddleware(next, _logger);

        var context = BuildContext(customAuthHeader: "Bearer some-token");
        await middleware.InvokeAsync(context);

        context.Items.ContainsKey("AgentId").Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_BearerWithSubClaim_SetsAgentId()
    {
        var agentId = Guid.NewGuid();
        var claims = new[] { new Claim("sub", agentId.ToString()) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        _authenticator.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(principal);

        var next = Substitute.For<RequestDelegate>();
        next(Arg.Any<HttpContext>()).Returns(Task.CompletedTask);
        var middleware = new Synentra.Middleware.AgentAuthMiddleware(next, _logger);

        var context = BuildContext(customAuthHeader: "Bearer some-token");
        await middleware.InvokeAsync(context);

        context.Items["AgentId"].Should().Be(agentId);
    }

    [Fact]
    public async Task InvokeAsync_AlwaysCallsNext()
    {
        var next = Substitute.For<RequestDelegate>();
        next(Arg.Any<HttpContext>()).Returns(Task.CompletedTask);
        var middleware = new Synentra.Middleware.AgentAuthMiddleware(next, _logger);

        var context = BuildContext();
        await middleware.InvokeAsync(context);

        await next.Received(1).Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_AuthorizationFallbackEnabled_UsesAuthorizationHeader()
    {
        var agentId = Guid.NewGuid();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, agentId.ToString()) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        _authenticator.ValidateAsync("fallback-token", Arg.Any<CancellationToken>())
            .Returns(principal);

        var next = Substitute.For<RequestDelegate>();
        next(Arg.Any<HttpContext>()).Returns(Task.CompletedTask);
        var middleware = new Synentra.Middleware.AgentAuthMiddleware(next, _logger);

        var context = BuildContext(authorizationHeader: "Bearer fallback-token", fallbackToAuthorization: true);
        await middleware.InvokeAsync(context);

        context.Items["AgentId"].Should().Be(agentId);
    }

    [Fact]
    public async Task InvokeAsync_AuthorizationFallbackDisabled_DoesNotUseAuthorizationHeader()
    {
        var next = Substitute.For<RequestDelegate>();
        next(Arg.Any<HttpContext>()).Returns(Task.CompletedTask);
        var middleware = new Synentra.Middleware.AgentAuthMiddleware(next, _logger);

        var context = BuildContext(authorizationHeader: "Bearer fallback-token", fallbackToAuthorization: false);
        await middleware.InvokeAsync(context);

        context.Items.ContainsKey("AgentId").Should().BeFalse();
        await _authenticator.DidNotReceive().ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_CustomHeaderPresent_DoesNotFallbackToAuthorization()
    {
        var next = Substitute.For<RequestDelegate>();
        next(Arg.Any<HttpContext>()).Returns(Task.CompletedTask);
        var middleware = new Synentra.Middleware.AgentAuthMiddleware(next, _logger);

        var context = BuildContext(
            customAuthHeader: "Basic not-bearer",
            authorizationHeader: "Bearer fallback-token",
            fallbackToAuthorization: true);

        await middleware.InvokeAsync(context);

        context.Items.ContainsKey("AgentId").Should().BeFalse();
        await _authenticator.DidNotReceive().ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
