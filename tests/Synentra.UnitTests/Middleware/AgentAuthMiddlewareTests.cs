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
        string customHeaderName = "Synentra-Authorization",
        ExternalIdentityProviderType provider = ExternalIdentityProviderType.Jwt,
        string jwtAgentIdClaimType = "sub")
    {
        var services = new ServiceCollection();
        services.AddSingleton(_authenticator);
        services.AddSingleton<IOptions<SecurityConfiguration>>(Options.Create(new SecurityConfiguration
        {
            AgentAuth = new AgentAuthConfiguration
            {
                UseCustomHeader = useCustomHeader,
                CustomHeaderName = customHeaderName,
                FallbackToAuthorization = fallbackToAuthorization,
                ExternalIdentity = new ExternalIdentityConfiguration
                {
                    Provider = provider,
                    Jwt = new JwtIdentityConfiguration
                    {
                    }
                }
            }
        }));
        var serviceProvider = services.BuildServiceProvider();

        var context = new DefaultHttpContext
        {
            RequestServices = serviceProvider
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

    [Fact]
    public async Task InvokeAsync_JwtProvider_UsesConfiguredAgentIdClaimType()
    {
        var agentId = Guid.NewGuid();
        var claims = new[] { new Claim("agent_id", agentId.ToString()) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        _authenticator.ValidateAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(principal);

        var next = Substitute.For<RequestDelegate>();
        next(Arg.Any<HttpContext>()).Returns(Task.CompletedTask);
        var middleware = new Synentra.Middleware.AgentAuthMiddleware(next, _logger);

        var context = BuildContext(
            customAuthHeader: "Bearer valid-token",
            provider: ExternalIdentityProviderType.Jwt,
            jwtAgentIdClaimType: "agent_id");

        await middleware.InvokeAsync(context);

        context.Items["AgentId"].Should().Be(agentId);
    }

    [Fact]
    public async Task InvokeAsync_JwtProvider_FallsBackToCommonAgentIdClaims()
    {
        var agentId = Guid.NewGuid();
        var claims = new[] { new Claim("agent_id", agentId.ToString()) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims));

        _authenticator.ValidateAsync("valid-token", Arg.Any<CancellationToken>())
            .Returns(principal);

        var next = Substitute.For<RequestDelegate>();
        next(Arg.Any<HttpContext>()).Returns(Task.CompletedTask);
        var middleware = new Synentra.Middleware.AgentAuthMiddleware(next, _logger);

        var context = BuildContext(
            customAuthHeader: "Bearer valid-token",
            provider: ExternalIdentityProviderType.Jwt,
            jwtAgentIdClaimType: "missing_claim");

        await middleware.InvokeAsync(context);

        context.Items["AgentId"].Should().Be(agentId);
    }

    [Fact]
    public async Task InvokeAsync_NoAgentAuthConfiguration_SkipsAuthentication()
    {
        var services = new ServiceCollection();

        services.AddSingleton(_authenticator);

        services.AddSingleton<IOptions<SecurityConfiguration>>(
            Options.Create(new SecurityConfiguration
            {
                AgentAuth = null
            }));

        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };

        var next = Substitute.For<RequestDelegate>();
        next(Arg.Any<HttpContext>()).Returns(Task.CompletedTask);

        var middleware = new Synentra.Middleware.AgentAuthMiddleware(next, _logger);

        await middleware.InvokeAsync(context);

        await _authenticator.DidNotReceive()
            .ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());

        await next.Received(1).Invoke(context);
    }

    [Fact]
    public async Task InvokeAsync_UsesDefaultHeader_WhenCustomHeaderNameIsEmpty()
    {
        var id = Guid.NewGuid();

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(new[]
            {
            new Claim(ClaimTypes.NameIdentifier, id.ToString())
            }));

        _authenticator
            .ValidateAsync("token", Arg.Any<CancellationToken>())
            .Returns(principal);

        var services = new ServiceCollection();

        services.AddSingleton(_authenticator);

        services.AddSingleton<IOptions<SecurityConfiguration>>(
            Options.Create(new SecurityConfiguration
            {
                AgentAuth = new AgentAuthConfiguration
                {
                    UseCustomHeader = true,
                    CustomHeaderName = ""
                }
            }));

        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };

        context.Request.Headers["Synentra-Authorization"] = "Bearer token";

        var middleware = new Synentra.Middleware.AgentAuthMiddleware(
            _ => Task.CompletedTask,
            _logger);

        await middleware.InvokeAsync(context);

        context.Items["AgentId"].Should().Be(id);
    }

    [Fact]
    public async Task InvokeAsync_CustomHeaderDisabled_DoesNotReadCustomHeader()
    {
        var context = BuildContext(
            customAuthHeader: "Bearer token",
            useCustomHeader: false);

        var middleware = new Synentra.Middleware.AgentAuthMiddleware(
            _ => Task.CompletedTask,
            _logger);

        await middleware.InvokeAsync(context);

        await _authenticator.DidNotReceive()
            .ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_BearerWithoutToken_DoesNothing()
    {
        var context = BuildContext(customAuthHeader: "Bearer");

        var middleware = new Synentra.Middleware.AgentAuthMiddleware(
            _ => Task.CompletedTask,
            _logger);

        await middleware.InvokeAsync(context);

        context.Items.Should().NotContainKey("AgentId");
    }

    [Fact]
    public async Task InvokeAsync_BearerWithExtraSpaces_IsAccepted()
    {
        var id = Guid.NewGuid();

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(new[]
            {
            new Claim(ClaimTypes.NameIdentifier, id.ToString())
            }));

        _authenticator.ValidateAsync("abc", Arg.Any<CancellationToken>())
            .Returns(principal);

        var context = BuildContext(
            customAuthHeader: "Bearer     abc");

        var middleware = new Synentra.Middleware.AgentAuthMiddleware(
            _ => Task.CompletedTask,
            _logger);

        await middleware.InvokeAsync(context);

        context.Items["AgentId"].Should().Be(id);
    }

    [Fact]
    public async Task InvokeAsync_AgentIdClaim_IsRecognized()
    {
        var id = Guid.NewGuid();

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(new[]
            {
            new Claim("agentId", id.ToString())
            }));

        _authenticator.ValidateAsync("token", Arg.Any<CancellationToken>())
            .Returns(principal);

        var context = BuildContext(customAuthHeader: "Bearer token");

        var middleware = new Synentra.Middleware.AgentAuthMiddleware(
            _ => Task.CompletedTask,
            _logger);

        await middleware.InvokeAsync(context);

        context.Items["AgentId"].Should().Be(id);
    }

    [Fact]
    public async Task InvokeAsync_ClientIdClaim_IsRecognized()
    {
        var id = Guid.NewGuid();

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(new[]
            {
            new Claim("client_id", id.ToString())
            }));

        _authenticator.ValidateAsync("token", Arg.Any<CancellationToken>())
            .Returns(principal);

        var context = BuildContext(customAuthHeader: "Bearer token");

        var middleware = new Synentra.Middleware.AgentAuthMiddleware(
            _ => Task.CompletedTask,
            _logger);

        await middleware.InvokeAsync(context);

        context.Items["AgentId"].Should().Be(id);
    }

    [Fact]
    public async Task InvokeAsync_InvalidTrustScore_DoesNotSetTrustScore()
    {
        var id = Guid.NewGuid();

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(new[]
            {
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim("trust_score", "abc")
            }));

        _authenticator.ValidateAsync("token", Arg.Any<CancellationToken>())
            .Returns(principal);

        var context = BuildContext(customAuthHeader: "Bearer token");

        var middleware = new Synentra.Middleware.AgentAuthMiddleware(
            _ => Task.CompletedTask,
            _logger);

        await middleware.InvokeAsync(context);

        context.Items.Should().ContainKey("AgentId");
        context.Items.Should().NotContainKey("TrustScore");
    }

    [Theory]
    [InlineData("bearer token")]
    [InlineData("BEARER token")]
    [InlineData("BeArEr token")]
    public async Task InvokeAsync_BearerScheme_IsCaseInsensitive(string header)
    {
        var id = Guid.NewGuid();

        _authenticator.ValidateAsync("token", Arg.Any<CancellationToken>())
            .Returns(new ClaimsPrincipal(
                new ClaimsIdentity(new[]
                {
                new Claim(ClaimTypes.NameIdentifier, id.ToString())
                })));

        var context = BuildContext(customAuthHeader: header);

        var middleware = new Synentra.Middleware.AgentAuthMiddleware(
            _ => Task.CompletedTask,
            _logger);

        await middleware.InvokeAsync(context);

        context.Items["AgentId"].Should().Be(id);
    }
}
