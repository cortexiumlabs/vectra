using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Synentra.BuildingBlocks.Configuration.Security;
using Synentra.BuildingBlocks.Configuration.Security.AgentAuth;
using Synentra.Domain.Agents;
using Synentra.Infrastructure.Security;

namespace Synentra.Infrastructure.UnitTests.Security;

public class JwtTokenServiceTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    /// <summary>
    /// Sets <c>SYNENTRA_PROTECTED_SECRET_PATH</c> to a temporary file,
    /// so the protected‑file fallback writes to an isolated location.
    /// Returns the path for cleanup.
    /// </summary>
    private string UseTempProtectedSecretPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"synentra-secret-{Guid.NewGuid():N}.protected");
        Environment.SetEnvironmentVariable("SYNENTRA_PROTECTED_SECRET_PATH", path);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        // Clean up environment variable and temp files
        Environment.SetEnvironmentVariable("SYNENTRA_PROTECTED_SECRET_PATH", null);
        Environment.SetEnvironmentVariable("SYNENTRA_SELF_SIGNED_SECRET", null);

        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); } catch { /* best effort */ }
        }
    }

    private static JwtTokenService CreateSut(
        string secret = "super-secret-key-for-testing-1234567890",
        string issuer = "synentra-issuer",
        string audience = "synentra-audience",
        TimeSpan? expiration = null,
        IDataProtectionProvider? dataProtectionProvider = null)
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

        dataProtectionProvider ??= new EphemeralDataProtectionProvider();
        return new JwtTokenService(Options.Create(config), dataProtectionProvider);
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
    public void GenerateToken_EmptySecret_AutoGeneratesSecretAndReturnsToken()
    {
        // Simulate that no secret is provided anywhere – protected file will be generated.
        UseTempProtectedSecretPath();
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
        var generatorSecret = Convert.ToBase64String(
            Enumerable.Repeat((byte)0x11, 32).ToArray());

        var validatorSecret = Convert.ToBase64String(
            Enumerable.Repeat((byte)0x22, 32).ToArray());

        var generator = CreateSut(secret: generatorSecret);
        var validator = CreateSut(secret: validatorSecret);

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
    public void ValidateToken_EmptySecret_SameInstance_Validates()
    {
        // Both generation and validation happen with the same auto‑generated secret.
        var protectedFilePath = UseTempProtectedSecretPath();
        var sut = CreateSut(secret: string.Empty);
        var agent = new Agent("TestAgent", "owner-1", "hash");
        var token = sut.GenerateToken(agent);

        var principal = sut.ValidateToken(token);

        principal.Should().NotBeNull();
    }

    [Fact]
    public void ValidateToken_EmptySecret_DifferentInstances_ShareSecretViaProtectedFile()
    {
        var protectedFilePath = UseTempProtectedSecretPath();

        // Use one provider so the key is the same for both instances
        var sharedProtectionProvider = new EphemeralDataProtectionProvider();

        var generator = CreateSut(secret: string.Empty, dataProtectionProvider: sharedProtectionProvider);
        var agent = new Agent("TestAgent", "owner-1", "hash");
        var token = generator.GenerateToken(agent);

        var validator = CreateSut(secret: string.Empty, dataProtectionProvider: sharedProtectionProvider);
        var principal = validator.ValidateToken(token);

        principal.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_MissingSecret_DoesNotThrow_AndGeneratesToken()
    {
        // In any environment, missing secret should auto‑generate, never throw.
        UseTempProtectedSecretPath();

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

        var sut = new JwtTokenService(Options.Create(config), new EphemeralDataProtectionProvider());
        var agent = new Agent("TestAgent", "owner-1", "hash");

        var token = sut.GenerateToken(agent);
        token.Should().NotBeNullOrEmpty();
    }
}