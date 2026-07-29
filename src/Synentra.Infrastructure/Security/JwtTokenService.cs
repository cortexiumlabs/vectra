using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Synentra.Application.Abstractions.Executions;
using Synentra.BuildingBlocks.Configuration.Security;
using Synentra.Domain.Agents;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Synentra.Infrastructure.Security;

public class JwtTokenService : ITokenService
{
    private const string SelfSignedSecretEnvVar = "SYNENTRA_SELF_SIGNED_SECRET";
    private static readonly Lock FileLock = new();

    private readonly string _secret;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly TimeSpan _expiration;

    public JwtTokenService(
        IOptions<SecurityConfiguration> authSettings,
        IDataProtectionProvider dataProtectionProvider)
    {
        var selfSigned = authSettings.Value?.AgentAuth?.TokenIssuance;

        _secret = ResolveSecret(selfSigned?.Secret, dataProtectionProvider);

        _issuer = !string.IsNullOrWhiteSpace(selfSigned?.Issuer)
            ? selfSigned.Issuer
            : "synentra";

        _audience = !string.IsNullOrWhiteSpace(selfSigned?.Audience)
            ? selfSigned.Audience
            : "synentra-agents";

        _expiration = selfSigned?.Expiration ?? TimeSpan.FromMinutes(15);
    }

    private static string ResolveSecret(
        string? configuredSecret,
        IDataProtectionProvider dataProtectionProvider)
    {
        string secret;

        if (!string.IsNullOrWhiteSpace(configuredSecret))
        {
            secret = configuredSecret;
        }
        else
        {
            var envSecret = Environment.GetEnvironmentVariable(SelfSignedSecretEnvVar);

            if (!string.IsNullOrWhiteSpace(envSecret))
            {
                secret = envSecret;
            }
            else if (TryGetProtectedSecret(dataProtectionProvider, out var existingSecret))
            {
                secret = existingSecret;
            }
            else
            {
                secret = CreateAndPersistProtectedSecret(dataProtectionProvider);
            }
        }

        if (Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new InvalidOperationException(
                "The JWT signing secret must be at least 32 bytes for HS256.");
        }

        return secret;
    }

    // --- Protected file handling ---

    private static string GetProtectedSecretFilePath()
    {
        // Allow override via environment variable
        var envPath = Environment.GetEnvironmentVariable("SYNENTRA_PROTECTED_SECRET_PATH");
        if (!string.IsNullOrWhiteSpace(envPath))
            return envPath;

        // Default: OS‑appropriate common application data folder
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
            appData = AppContext.BaseDirectory;

        return Path.Combine(appData, "Synentra", "Security", "AgentAuth", "agentauth-secret.protected");
    }

    private static bool TryGetProtectedSecret(
        IDataProtectionProvider provider, out string secret)
    {
        secret = string.Empty;
        var filePath = GetProtectedSecretFilePath();
        if (!File.Exists(filePath))
            return false;

        try
        {
            var protectedBytes = File.ReadAllBytes(filePath);
            var protector = provider.CreateProtector("Synentra.AgentAuth.Secret");
            var unprotectedBytes = protector.Unprotect(protectedBytes);
            secret = Encoding.UTF8.GetString(unprotectedBytes);
            return !string.IsNullOrWhiteSpace(secret);
        }
        catch
        {
            // Decryption failed → treat as missing (e.g., key rotated or file from another machine)
            return false;
        }
    }

    private static string CreateAndPersistProtectedSecret(
        IDataProtectionProvider provider)
    {
        var filePath = GetProtectedSecretFilePath();
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        lock (FileLock)
        {
            // Double-check inside lock to prevent race
            if (TryGetProtectedSecret(provider, out var alreadyCreated))
                return alreadyCreated;

            // Generate a cryptographically strong 64-byte secret
            var newSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            var secretBytes = Encoding.UTF8.GetBytes(newSecret);

            var protector = provider.CreateProtector("Synentra.AgentAuth.Secret");
            var protectedBytes = protector.Protect(secretBytes);

            // Atomic write: temp file + rename
            var tempFile = filePath + ".tmp";
            File.WriteAllBytes(tempFile, protectedBytes);
            File.Move(tempFile, filePath, overwrite: true);

            return newSecret;
        }
    }

    public string GenerateToken(Agent agent)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, agent.Id.ToString()),
            new Claim("agent_name", agent.Name),
            new Claim("trust_score", agent.TrustScore.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(_expiration),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_secret);
        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);
            return principal;
        }
        catch
        {
            return null;
        }
    }
}