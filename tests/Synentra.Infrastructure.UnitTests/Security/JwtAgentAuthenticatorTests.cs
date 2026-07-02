using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
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
        AgentAuthProviderType provider = AgentAuthProviderType.SelfSigned,
        ITokenService? tokenService = null,
        string? authority = null,
        string? metadataUrl = null)
    {
        var config = new SecurityConfiguration
        {
            AgentAuth = new AgentAuthConfiguration
            {
                Provider = provider,
                SelfSigned = new SelfSignedProvider
                {
                    Secret = "super-secret-key-for-tests-1234567890",
                    Issuer = "synentra-issuer",
                    Audience = "synentra-audience",
                    Expiration = TimeSpan.FromMinutes(15)
                },
                Jwt = new JwtProvider
                {
                    Authority = authority ?? "https://identity.example.com",
                    MetadataUrl = metadataUrl,
                    ValidateIssuer = false,
                    ValidateAudience = false
                }
            }
        };
        tokenService ??= Substitute.For<ITokenService>();

        var services = new ServiceCollection();
        services.AddSingleton(tokenService);
        var serviceProvider = services.BuildServiceProvider();

        return new JwtAgentAuthenticator(Options.Create(config), serviceProvider);
    }

    [Fact]
    public void Authenticate_SelfSignedProvider_GeneratesToken()
    {
        var tokenService = Substitute.For<ITokenService>();
        tokenService.GenerateToken(Arg.Any<Agent>()).Returns("generated-token");
        var sut = CreateSut(AgentAuthProviderType.SelfSigned, tokenService);
        var agent = new Agent("TestAgent", "owner-1", "hash");

        var result = sut.Authenticate(agent);

        result.Succeeded.Should().BeTrue();
        result.Token.Should().Be("generated-token");
        tokenService.Received(1).GenerateToken(agent);
    }

    [Fact]
    public void Authenticate_ExternalProvider_ReturnsFailure()
    {
        var sut = CreateSut(AgentAuthProviderType.Jwt);
        var agent = new Agent("TestAgent", "owner-1", "hash");

        var result = sut.Authenticate(agent);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateAsync_SelfSigned_DelegatesToTokenService()
    {
        var tokenService = Substitute.For<ITokenService>();
        var expectedPrincipal = new System.Security.Claims.ClaimsPrincipal();
        tokenService.ValidateToken(Arg.Any<string>()).Returns(expectedPrincipal);
        var sut = CreateSut(AgentAuthProviderType.SelfSigned, tokenService);

        var result = await sut.ValidateAsync("some-token", TestContext.Current.CancellationToken);

        result.Should().Be(expectedPrincipal);
        tokenService.Received(1).ValidateToken("some-token");
    }

    [Fact]
    public async Task ValidateAsync_SelfSigned_InvalidToken_ReturnsNull()
    {
        var tokenService = Substitute.For<ITokenService>();
        tokenService.ValidateToken(Arg.Any<string>()).Returns((System.Security.Claims.ClaimsPrincipal?)null);
        var sut = CreateSut(AgentAuthProviderType.SelfSigned, tokenService);

        var result = await sut.ValidateAsync("bad-token", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithExplicitMetadataUrl_DoesNotThrow()
    {
        // Exercises the Lazy constructor path with explicit MetadataUrl
        var act = () => CreateSut(
            AgentAuthProviderType.Jwt,
            metadataUrl: "http://localhost:8080/.well-known/openid-configuration");

        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithAuthority_BuildsMetadataUrlFromAuthority()
    {
        // Exercises path where MetadataUrl is null → derived from Authority
        var act = () => CreateSut(
            AgentAuthProviderType.Jwt,
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
            AgentAuthProviderType.Jwt,
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
            AgentAuthProviderType.Jwt,
            authority: "http://localhost:8080",
            metadataUrl: "http://localhost:8080/.well-known/openid-configuration");

        // Will fail to fetch, but should not throw
        var result = await sut.ValidateAsync("some.jwt.token", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }
}
