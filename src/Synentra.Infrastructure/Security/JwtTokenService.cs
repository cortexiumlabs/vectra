using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Synentra.Application.Abstractions.Executions;
using Synentra.Domain.Agents;
using Synentra.BuildingBlocks.Configuration.Security;

namespace Synentra.Infrastructure.Security;

public class JwtTokenService : ITokenService
{
    private const string SelfSignedSecretEnvironmentVariable = "SYNENTRA_SELF_SIGNED_SECRET";
    private const string DevSecretFileEnvironmentVariable = "SYNENTRA_SELF_SIGNED_DEV_SECRET_FILE";
    private const string DevSecretFileName = ".synentra-agentauth-selfsigned-secret";
    private const string DefaultIssuer = "synentra";
    private const string DefaultAudience = "synentra-agents";
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(15);
    private static readonly Lock DevSecretFileLock = new();

    private readonly string _secret;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly TimeSpan _expiration;

    public JwtTokenService(IOptions<SecurityConfiguration> authSettings, IHostEnvironment? hostEnvironment = null)
    {
        var selfSigned = authSettings.Value?.AgentAuth?.SelfSigned;

        _secret = ResolveSecret(selfSigned?.Secret, hostEnvironment);

        _issuer = !string.IsNullOrWhiteSpace(selfSigned?.Issuer)
            ? selfSigned.Issuer
            : DefaultIssuer;

        _audience = !string.IsNullOrWhiteSpace(selfSigned?.Audience)
            ? selfSigned.Audience
            : DefaultAudience;

        _expiration = selfSigned?.Expiration ?? DefaultExpiration;
    }

    private static string ResolveSecret(string? configuredSecret, IHostEnvironment? hostEnvironment)
    {
        if (!string.IsNullOrWhiteSpace(configuredSecret))
            return configuredSecret;

        var envSecret = Environment.GetEnvironmentVariable(SelfSignedSecretEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envSecret))
            return envSecret;

        var isDevelopment = hostEnvironment?.IsDevelopment() ?? true;
        if (isDevelopment)
            return GetOrCreateDevelopmentSecret();

        throw new InvalidOperationException(
            $"Self-signed token secret is missing. Configure Security:AgentAuth:SelfSigned:Secret or set {SelfSignedSecretEnvironmentVariable}.");
    }

    private static string GetOrCreateDevelopmentSecret()
    {
        var secretFilePath = GetDevelopmentSecretFilePath();

        lock (DevSecretFileLock)
        {
            if (TryReadExistingSecret(secretFilePath, out var existingSecret))
                return existingSecret;

            var directory = Path.GetDirectoryName(secretFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var generatedSecret = GenerateRandomSecret();

            File.WriteAllText(secretFilePath, generatedSecret);

            return generatedSecret;
        }
    }

    private static bool TryReadExistingSecret(string secretFilePath, out string secret)
    {
        secret = string.Empty;

        if (!File.Exists(secretFilePath))
            return false;

        var existing = File.ReadAllText(secretFilePath).Trim();
        if (string.IsNullOrWhiteSpace(existing))
            return false;

        secret = existing;
        return true;
    }

    private static string GetDevelopmentSecretFilePath()
    {
        var overridePath = Environment.GetEnvironmentVariable(DevSecretFileEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
            return overridePath;

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
            userProfile = AppContext.BaseDirectory;

        return Path.Combine(userProfile, DevSecretFileName);
    }

    private static string GenerateRandomSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
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