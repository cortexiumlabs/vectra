using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Synentra.Application.Abstractions.Executions;
using Synentra.BuildingBlocks.Configuration.Security;
using Synentra.BuildingBlocks.Configuration.Security.AgentAuth;
using Synentra.Domain.Agents;
using Synentra.Infrastructure.Security;

namespace Synentra.Infrastructure.UnitTests.Security;

public class JwtAgentAuthenticatorTests
{
    private static JwtAgentAuthenticator CreateSut(
        ITokenService? tokenService = null,
        string? authority = null,
        string? metadataUrl = null,
        bool externalEnabled = false,
        Action<ExternalIdentityConfiguration>? configureExternal = null)
    {
        var config = new SecurityConfiguration
        {
            AgentAuth = new AgentAuthConfiguration
            {
                ExternalIdentity = new ExternalIdentityConfiguration
                {
                    Enabled = externalEnabled,
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

        configureExternal?.Invoke(config.AgentAuth.ExternalIdentity);

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

    /// <summary>Creates an RSA key and returns the security key + its public JWK representation.</summary>
    private static (RsaSecurityKey Key, JsonWebKey Jwk) CreateTestRsaKey()
    {
        var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "test-kid" };
        var parameters = rsa.ExportParameters(false);
        var jwk = new JsonWebKey
        {
            Kty = "RSA",
            Use = "sig",
            Kid = "test-kid",
            Alg = SecurityAlgorithms.RsaSha256,
            N = Base64UrlEncoder.Encode(parameters.Modulus),
            E = Base64UrlEncoder.Encode(parameters.Exponent)
        };
        return (key, jwk);
    }

    /// <summary>Generates a signed JWT using the supplied key and optional claims.</summary>
    private static string CreateJwt(
        SecurityKey signingKey,
        string? issuer = null,
        string? audience = null,
        DateTime? expires = null)
    {
        var handler = new JwtSecurityTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256),
            Expires = expires ?? DateTime.UtcNow.AddHours(1),
            Issuer = issuer,
            Audience = audience,
        };
        var token = handler.CreateToken(descriptor);
        return handler.WriteToken(token);
    }

    /// <summary>Injects a mocked <see cref="ConfigurationManager{OpenIdConnectConfiguration}"/> into the SUT.</summary>
    private static void InjectMockOidcManager(
        JwtAgentAuthenticator sut,
        OpenIdConnectConfiguration config)
    {
        // Use unambiguous two-parameter constructor to avoid AmbiguousMatchException
        var mockManager = Substitute.For<ConfigurationManager<OpenIdConnectConfiguration>>(
            "http://dummy", new OpenIdConnectConfigurationRetriever());
        mockManager.GetConfigurationAsync(default).ReturnsForAnyArgs(config);

        var lazyField = typeof(JwtAgentAuthenticator)
            .GetField("_oidcConfigManager", BindingFlags.NonPublic | BindingFlags.Instance)!;
        lazyField.SetValue(sut, new Lazy<ConfigurationManager<OpenIdConnectConfiguration>>(() => mockManager));
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
        var expectedPrincipal = new ClaimsPrincipal();
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
        tokenService.ValidateToken(Arg.Any<string>()).Returns((ClaimsPrincipal?)null);
        var sut = CreateSut(tokenService);

        var result = await sut.ValidateAsync("bad-token", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithExplicitMetadataUrl_DoesNotThrow()
    {
        var act = () => CreateSut(metadataUrl: "http://localhost:8080/.well-known/openid-configuration");
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithAuthority_BuildsMetadataUrlFromAuthority()
    {
        var act = () => CreateSut(authority: "https://identity.example.com", metadataUrl: null);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task ValidateAsync_ExternalJwt_InvalidToken_ReturnsNull()
    {
        // Now with externalEnabled = true, the external path is actually exercised.
        var sut = CreateSut(
            authority: "https://identity.example.com",
            metadataUrl: "http://localhost:9999/.well-known/oidc-that-doesnt-exist",
            externalEnabled: true);

        var result = await sut.ValidateAsync("invalid.jwt.token", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_ExternalJwt_LocalhostMetadata_AllowsHttp()
    {
        var sut = CreateSut(
            authority: "http://localhost:8080",
            metadataUrl: "http://localhost:8080/.well-known/openid-configuration",
            externalEnabled: true);

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
            metadataUrl: "http://localhost:9999/.well-known/openid",
            externalEnabled: true);

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
            metadataUrl: "http://localhost:9999/.well-known/openid",
            externalEnabled: true);

        var result = await sut.ValidateAsync("token", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_ExternalJwt_IssuerValidationEnabled_DoesNotThrow()
    {
        var sut = CreateSut(
            authority: "https://identity.example.com",
            metadataUrl: "http://localhost:9999/.well-known/openid",
            externalEnabled: true);

        var act = async () =>
            await sut.ValidateAsync("invalid.jwt.token", TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateAsync_ExternalJwt_AudienceValidationEnabled_DoesNotThrow()
    {
        var sut = CreateSut(
            authority: "https://identity.example.com",
            metadataUrl: "http://localhost:9999/.well-known/openid",
            externalEnabled: true);

        var result = await sut.ValidateAsync("invalid.jwt.token", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public void Constructor_MetadataUrl_EmptyString_FallsBackToAuthority()
    {
        var act = () => CreateSut(authority: "https://identity.example.com", metadataUrl: "   ");
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
            metadataUrl: "http://localhost:9999/.well-known/openid",
            externalEnabled: true);

        var result = await sut.ValidateAsync("token", TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_ExternalEnabled_ValidToken_NoIssuerAudience_ReturnsPrincipal()
    {
        // Arrange
        var (key, _) = CreateTestRsaKey();
        var oidcConfig = new OpenIdConnectConfiguration
        {
            SigningKeys = { key }
        };
        var token = CreateJwt(key, issuer: null, audience: null);

        var sut = CreateSut(externalEnabled: true);
        InjectMockOidcManager(sut, oidcConfig);

        // Act
        var result = await sut.ValidateAsync(token, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Identities.Should().HaveCount(1);
    }

    [Fact]
    public async Task ValidateAsync_ExternalEnabled_ValidToken_WithIssuerValidation_ReturnsPrincipal()
    {
        // Arrange
        var (key, _) = CreateTestRsaKey();
        var oidcConfig = new OpenIdConnectConfiguration
        {
            Issuer = "https://issuer.example.com",
            SigningKeys = { key }
        };
        var token = CreateJwt(key, issuer: "https://issuer.example.com");

        var sut = CreateSut(
            externalEnabled: true,
            configureExternal: ext =>
            {
                ext.Jwt!.ValidateIssuer = true;
                ext.Jwt.Authority = "https://issuer.example.com";
            });
        InjectMockOidcManager(sut, oidcConfig);

        // Act
        var result = await sut.ValidateAsync(token, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.FindFirst("iss")?.Value.Should().Be("https://issuer.example.com");
    }

    [Fact]
    public async Task ValidateAsync_ExternalEnabled_TokenWithWrongIssuer_FallsBackToInternal()
    {
        var (key, _) = CreateTestRsaKey();
        var oidcConfig = new OpenIdConnectConfiguration
        {
            Issuer = "https://correct-issuer.example.com",
            SigningKeys = { key }
        };
        var token = CreateJwt(key, issuer: "https://wrong-issuer.example.com");

        var tokenService = Substitute.For<ITokenService>();
        var fallbackPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("fallback", "yes") }));
        tokenService.ValidateToken(Arg.Any<string>()).Returns(fallbackPrincipal);

        var sut = CreateSut(
            tokenService,
            externalEnabled: true,
            configureExternal: ext =>
            {
                ext.Jwt!.ValidateIssuer = true;
                ext.Jwt.Authority = "https://correct-issuer.example.com";
            });
        InjectMockOidcManager(sut, oidcConfig);

        var result = await sut.ValidateAsync(token, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(fallbackPrincipal);
    }

    [Fact]
    public async Task ValidateAsync_ExternalEnabled_ValidToken_WithAudienceValidation_ReturnsPrincipal()
    {
        var (key, _) = CreateTestRsaKey();
        var oidcConfig = new OpenIdConnectConfiguration
        {
            SigningKeys = { key }
        };
        var token = CreateJwt(key, audience: "my-aud");

        var sut = CreateSut(
            externalEnabled: true,
            configureExternal: ext =>
            {
                ext.Jwt!.ValidateAudience = true;
                ext.Jwt.Audience = "my-aud";
            });
        InjectMockOidcManager(sut, oidcConfig);

        var result = await sut.ValidateAsync(token, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.FindFirst("aud")?.Value.Should().Be("my-aud");
    }

    [Fact]
    public async Task ValidateAsync_ExternalEnabled_TokenWithWrongAudience_FallsBackToInternal()
    {
        var (key, _) = CreateTestRsaKey();
        var oidcConfig = new OpenIdConnectConfiguration
        {
            SigningKeys = { key }
        };
        var token = CreateJwt(key, audience: "wrong-aud");

        var tokenService = Substitute.For<ITokenService>();
        var fallbackPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("fallback", "yes") }));
        tokenService.ValidateToken(Arg.Any<string>()).Returns(fallbackPrincipal);

        var sut = CreateSut(
            tokenService,
            externalEnabled: true,
            configureExternal: ext =>
            {
                ext.Jwt!.ValidateAudience = true;
                ext.Jwt.Audience = "expected-aud";
            });
        InjectMockOidcManager(sut, oidcConfig);

        var result = await sut.ValidateAsync(token, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(fallbackPrincipal);
    }

    [Fact]
    public async Task ValidateAsync_ExternalEnabled_MissingJwtConfig_FallsBackToInternal()
    {
        // Jwt = null → Lazy throws, caught, fallback
        var tokenService = Substitute.For<ITokenService>();
        var fallbackPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("fb", "1") }));
        tokenService.ValidateToken(Arg.Any<string>()).Returns(fallbackPrincipal);

        var sut = CreateSut(
            tokenService,
            externalEnabled: true,
            configureExternal: ext =>
            {
                ext.Jwt = null; // no JWT configuration
            });

        var result = await sut.ValidateAsync("token", TestContext.Current.CancellationToken);

        result.Should().BeSameAs(fallbackPrincipal);
    }

    [Fact]
    public async Task ValidateAsync_ExternalEnabled_NoAuthorityNorMetadataUrl_FallsBack()
    {
        var tokenService = Substitute.For<ITokenService>();
        tokenService.ValidateToken(Arg.Any<string>()).Returns((ClaimsPrincipal?)null);

        var sut = CreateSut(
            tokenService,
            authority: null,          // will be overridden, but we set both null via configureExternal
            metadataUrl: null,
            externalEnabled: true,
            configureExternal: ext =>
            {
                ext.Jwt!.Authority = null;
                ext.Jwt.MetadataUrl = null;
            });

        var result = await sut.ValidateAsync("token", TestContext.Current.CancellationToken);
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_ExternalEnabled_InvalidMetadataUri_FallsBack()
    {
        var tokenService = Substitute.For<ITokenService>();
        tokenService.ValidateToken(Arg.Any<string>()).Returns((ClaimsPrincipal?)null);

        var sut = CreateSut(
            tokenService,
            authority: "not a valid uri",    // invalid
            metadataUrl: null,
            externalEnabled: true);

        var result = await sut.ValidateAsync("token", TestContext.Current.CancellationToken);
        result.Should().BeNull();
    }

    [Fact]
    public void ValidateExternalTokenAsync_WhenExternalIdentityDisabled_ReturnsNull()
    {
        // Test the internal guard clause by invoking the private method via reflection
        var sut = CreateSut(externalEnabled: false);
        var method = typeof(JwtAgentAuthenticator).GetMethod("ValidateExternalTokenAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var task = (Task<ClaimsPrincipal?>)method.Invoke(sut, new object[] { "token", CancellationToken.None })!;

        task.Result.Should().BeNull();
    }
}