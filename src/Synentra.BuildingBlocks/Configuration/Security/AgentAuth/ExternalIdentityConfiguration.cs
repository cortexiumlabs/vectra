namespace Synentra.BuildingBlocks.Configuration.Security.AgentAuth;

public class ExternalIdentityConfiguration
{
    public bool? Enabled { get; set; } = false;
    public ExternalIdentityProviderType Provider { get; set; } = ExternalIdentityProviderType.Jwt;
    public JwtIdentityConfiguration Jwt { get; set; } = new();
}
