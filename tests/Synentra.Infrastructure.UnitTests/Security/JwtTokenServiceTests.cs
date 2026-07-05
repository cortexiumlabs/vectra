using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Synentra.BuildingBlocks.Configuration.Security;
using Synentra.BuildingBlocks.Configuration.Security.AgentAuth;
using Synentra.Domain.Agents;
using Synentra.Infrastructure.Security;

namespace Synentra.Infrastructure.UnitTests.Security;

public class JwtTokenServiceTests
{
    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Synentra.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private static JwtTokenService CreateSut(
        string secret = "super-secret-key-for-testing-1234567890",
        string issuer = "synentra-issuer",
        string audience = "synentra-audience",
        TimeSpan? expiration = null,
        IHostEnvironment? hostEnvironment = null)
    {
        var config = new SecurityConfiguration
        {
            AgentAuth = new AgentAuthConfiguration
            {
                TokenIssuance = new TokenIssuanceConfiguration
                {
                    Secret = secret,
                    Issuer = issuer,
                    Audience = audience,
                    Expiration = expiration ?? TimeSpan.FromMinutes(15)
                }
            }
        };
        return new JwtTokenService(Options.Create(config), hostEnvironment);
    }

    [Fact]
    public void GenerateToken_ValidAgent_ReturnsNonEmptyToken()
    {
        var sut = CreateSut();
        var agent = new Agent("TestAgent", "owner-1", "hash");

        var token = sut.GenerateToken(agent);

        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateToken_EmptySecret_UsesDefaultSecretAndReturnsToken()
    {
        var sut = CreateSut(secret: string.Empty);
        var agent = new Agent("TestAgent", "owner-1", "hash");

        var token = sut.GenerateToken(agent);

        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ValidateToken_ValidToken_ReturnsPrincipalWithClaims()
    {
        var sut = CreateSut();
        var agent = new Agent("TestAgent", "owner-1", "hash");
        var token = sut.GenerateToken(agent);

        var principal = sut.ValidateToken(token);

        principal.Should().NotBeNull();
        principal!.FindFirst("agent_name")?.Value.Should().Be("TestAgent");
        principal.FindFirst("trust_score")?.Value.Should().Be(agent.TrustScore.ToString());
    }

    [Fact]
    public void ValidateToken_InvalidToken_ReturnsNull()
    {
        var sut = CreateSut();

        var principal = sut.ValidateToken("this.is.not.a.valid.jwt");

        principal.Should().BeNull();
    }

    [Fact]
    public void ValidateToken_TokenSignedWithDifferentSecret_ReturnsNull()
    {
        var generator = CreateSut(secret: "super-secret-key-for-testing-1234567890");
        var validator = CreateSut(secret: "different-secret-key-for-testing-9999999");
        var agent = new Agent("TestAgent", "owner-1", "hash");
        var token = generator.GenerateToken(agent);

        var principal = validator.ValidateToken(token);

        principal.Should().BeNull();
    }

    [Fact]
    public void ValidateToken_ExpiredToken_ReturnsNull()
    {
        var sut = CreateSut(expiration: TimeSpan.FromSeconds(-1));
        var agent = new Agent("TestAgent", "owner-1", "hash");
        var token = sut.GenerateToken(agent);

        var principal = sut.ValidateToken(token);

        principal.Should().BeNull();
    }

    [Fact]
    public void ValidateToken_EmptySecret_UsesDefaultSecretAndReturnsPrincipal()
    {
        var generator = CreateSut(secret: string.Empty);
        var validator = CreateSut(secret: string.Empty);
        var agent = new Agent("TestAgent", "owner-1", "hash");
        var token = generator.GenerateToken(agent);

        var principal = validator.ValidateToken(token);

        principal.Should().NotBeNull();
    }

    [Fact]
    public void ValidateToken_MissingSelfSignedConfiguration_UsesDeterministicDefaults()
    {
        var secretFile = Path.Combine(Path.GetTempPath(), $"synentra-jwt-secret-{Guid.NewGuid():N}.txt");
        Environment.SetEnvironmentVariable("SYNENTRA_SELF_SIGNED_DEV_SECRET_FILE", secretFile);
        Environment.SetEnvironmentVariable("SYNENTRA_SELF_SIGNED_SECRET", null);
        try
        {
            var generatorConfig = new SecurityConfiguration { AgentAuth = new AgentAuthConfiguration { TokenIssuance = null! } };
            var validatorConfig = new SecurityConfiguration { AgentAuth = new AgentAuthConfiguration { TokenIssuance = null! } };

            var devEnv = new FakeHostEnvironment { EnvironmentName = Environments.Development };
            var generator = new JwtTokenService(Options.Create(generatorConfig), devEnv);
            var validator = new JwtTokenService(Options.Create(validatorConfig), devEnv);
            var agent = new Agent("TestAgent", "owner-1", "hash");
            var token = generator.GenerateToken(agent);

            var principal = validator.ValidateToken(token);

            principal.Should().NotBeNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable("SYNENTRA_SELF_SIGNED_DEV_SECRET_FILE", null);
            if (File.Exists(secretFile))
                File.Delete(secretFile);
        }
    }

    [Fact]
    public void Constructor_NonDevelopmentAndMissingSecret_ThrowsInvalidOperationException()
    {
        Environment.SetEnvironmentVariable("SYNENTRA_SELF_SIGNED_SECRET", null);
        var prodEnv = new FakeHostEnvironment { EnvironmentName = Environments.Production };

        var config = new SecurityConfiguration
        {
            AgentAuth = new AgentAuthConfiguration
            {
                TokenIssuance = new TokenIssuanceConfiguration
                {
                    Secret = string.Empty,
                    Issuer = "synentra-issuer",
                    Audience = "synentra-audience",
                    Expiration = TimeSpan.FromMinutes(15)
                }
            }
        };

        var act = () => new JwtTokenService(Options.Create(config), prodEnv);

        act.Should().Throw<InvalidOperationException>();
    }
}
