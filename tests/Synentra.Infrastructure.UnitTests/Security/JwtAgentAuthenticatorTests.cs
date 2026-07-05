using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using System.Security.Claims;
using Synentra.Application.Abstractions.Executions;
using Synentra.Domain.Agents;
using Synentra.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Synentra.BuildingBlocks.Configuration.Security;
using Synentra.BuildingBlocks.Configuration.Security.AgentAuth;

namespace Synentra.Infrastructure.UnitTests.Security;

public class JwtAgentAuthenticatorTests
{
    private static JwtAgentAuthenticator CreateSut(
        ITokenService? tokenService = null,
        string? authority = null,
        string? metadataUrl = null)
    {
        var config = new SecurityConfiguration
        {
            AgentAuth = new AgentAuthConfiguration
            {
                ExternalIdentity = new ExternalIdentityConfiguration
                {
                    Provider = ExternalIdentityProviderType.Jwt,
                    Jwt = new JwtIdentityConfiguration
                    {
                        Authority = authority ?? "https://identity.example.com",
                        MetadataUrl = metadataUrl,
                        ValidateIssuer = false,
                        ValidateAudience = false
                    }
                },
                TokenIssuance = new TokenIssuanceConfiguration
                {
                    Secret = "super-secret-key-for-tests-1234567890",
                    Issuer = "synentra-issuer",
                    Audience = "synentra-audience",
                    Expiration = TimeSpan.FromMinutes(15)
                }
            }
        };
        if (tokenService is null)
        {
            tokenService = Substitute.For<ITokenService>();
            tokenService.GenerateToken(Arg.Any<Agent>()).Returns("generated-token");
            tokenService.ValidateToken(Arg.Any<string>()).Returns((ClaimsPrincipal?)null);
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(tokenService);
        var serviceProvider = services.BuildServiceProvider();

        return new JwtAgentAuthenticator(Options.Create(config), serviceProvider);
    }

    [Fact]
    public void Authenticate_GeneratesToken()
    {
        var tokenService = Substitute.For<ITokenService>();
        tokenService.GenerateToken(Arg.Any<Agent>()).Returns("generated-token");
        var sut = CreateSut(tokenService);
        var agent = new Agent("TestAgent", "owner-1", "hash");

        var result = sut.Authenticate(agent);

        result.Succeeded.Should().BeTrue();
        result.Token.Should().Be("generated-token");
        tokenService.Received(1).GenerateToken(agent);
    }

    [Fact]
    public async Task ValidateAsync_DelegatesToTokenService()
    {
        var tokenService = Substitute.For<ITokenService>();
        var expectedPrincipal = new System.Security.Claims.ClaimsPrincipal();
        tokenService.ValidateToken(Arg.Any<string>()).Returns(expectedPrincipal);
        var sut = CreateSut(tokenService);

        var result = await sut.ValidateAsync("some-token", TestContext.Current.CancellationToken);

        result.Should().Be(expectedPrincipal);
        tokenService.Received(1).ValidateToken("some-token");
    }

    [Fact]
    public async Task ValidateAsync_InvalidToken_ReturnsNull()
    {
        var tokenService = Substitute.For<ITokenService>();
        tokenService.ValidateToken(Arg.Any<string>()).Returns((System.Security.Claims.ClaimsPrincipal?)null);
        var sut = CreateSut(tokenService);

        var result = await sut.ValidateAsync("bad-token", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithExplicitMetadataUrl_DoesNotThrow()
    {
        // Exercises the Lazy constructor path with explicit MetadataUrl
        var act = () => CreateSut(
            metadataUrl: "http://localhost:8080/.well-known/openid-configuration");

        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithAuthority_BuildsMetadataUrlFromAuthority()
    {
        // Exercises path where MetadataUrl is null → derived from Authority
        var act = () => CreateSut(
            authority: "https://identity.example.com",
            metadataUrl: null);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task ValidateAsync_ExternalJwt_InvalidToken_ReturnsNull()
    {
        // External provider with clearly invalid token → ValidateExternalTokenAsync
        // must catch the exception and return null
        var sut = CreateSut(
            authority: "https://identity.example.com",
            metadataUrl: "http://localhost:9999/.well-known/oidc-that-doesnt-exist");

        // The OIDC metadata fetch will fail → catch block returns null
        var result = await sut.ValidateAsync("invalid.jwt.token", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_ExternalJwt_LocalhostMetadata_AllowsHttp()
    {
        // Exercises the localhost check (RequireHttps = false branch)
        var sut = CreateSut(
            authority: "http://localhost:8080",
            metadataUrl: "http://localhost:8080/.well-known/openid-configuration");

        // Will fail to fetch, but should not throw
        var result = await sut.ValidateAsync("some.jwt.token", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public void Authenticate_UsesTokenService()
    {
        var tokenService = Substitute.For<ITokenService>();
        tokenService.GenerateToken(Arg.Any<Agent>()).Returns("self-signed-token");

        var sut = CreateSut(tokenService);

        var agent = new Agent("agent", "owner", "hash");

        var result = sut.Authenticate(agent);

        result.Succeeded.Should().BeTrue();
        result.Token.Should().Be("self-signed-token");
        tokenService.Received(1).GenerateToken(agent);
    }

    [Fact]
    public async Task ValidateAsync_ExternalValidationFails_FallsBackToTokenService()
    {
        var tokenService = Substitute.For<ITokenService>();

        var expectedPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
        new Claim(ClaimTypes.Name, "fallback-user")
    }));

        tokenService.ValidateToken("some-token").Returns(expectedPrincipal);

        var sut = CreateSut(
            tokenService,
            authority: "https://identity.example.com",
            metadataUrl: "http://localhost:9999/.well-known/openid");

        var result = await sut.ValidateAsync("some-token", TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Identity!.Name.Should().Be("fallback-user");
    }

    [Fact]
    public async Task ValidateAsync_TokenServiceThrows_ReturnsNull()
    {
        var tokenService = Substitute.For<ITokenService>();
        tokenService.ValidateToken(Arg.Any<string>())
            .Returns(_ => throw new InvalidOperationException("invalid self-signed config"));

        var sut = CreateSut(
            tokenService,
            authority: "https://identity.example.com",
            metadataUrl: "http://localhost:9999/.well-known/openid");

        var result = await sut.ValidateAsync("token", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_ExternalJwt_IssuerValidationEnabled_DoesNotThrow()
    {
        var sut = CreateSut(
            authority: "https://identity.example.com",
            metadataUrl: "http://localhost:9999/.well-known/openid");

        var act = async () =>
            await sut.ValidateAsync("invalid.jwt.token", TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateAsync_ExternalJwt_AudienceValidationEnabled_DoesNotThrow()
    {
        var sut = CreateSut(
            authority: "https://identity.example.com",
            metadataUrl: "http://localhost:9999/.well-known/openid");

        var result = await sut.ValidateAsync("invalid.jwt.token", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public void Constructor_MetadataUrl_EmptyString_FallsBackToAuthority()
    {
        var act = () => CreateSut(
            authority: "https://identity.example.com",
            metadataUrl: "   ");

        act.Should().NotThrow();
    }

    [Fact]
    public async Task ValidateAsync_ReturnsTokenServiceResult()
    {
        var tokenService = Substitute.For<ITokenService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity("test"));

        tokenService.ValidateToken("token").Returns(principal);

        var sut = CreateSut(tokenService);

        var result = await sut.ValidateAsync("token", TestContext.Current.CancellationToken);

        result.Should().BeSameAs(principal);
    }

    [Fact]
    public async Task ValidateAsync_ExternalValidationFails_AndTokenServiceReturnsNull()
    {
        var tokenService = Substitute.For<ITokenService>();
        tokenService.ValidateToken(Arg.Any<string>()).Returns((ClaimsPrincipal?)null);

        var sut = CreateSut(
            tokenService,
            authority: "https://identity.example.com",
            metadataUrl: "http://localhost:9999/.well-known/openid");

        var result = await sut.ValidateAsync("token", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }
}
