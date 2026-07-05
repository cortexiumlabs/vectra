namespace Synentra.BuildingBlocks.Configuration.Security.AgentAuth;

/// <summary>
/// External provider settings (Keycloak, Auth0, Azure AD, Custom)
/// </summary>
public class JwtIdentityConfiguration
{
    public string Authority { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string? MetadataUrl { get; set; } = string.Empty;
    public bool ValidateIssuer { get; set; } = false;
    public bool ValidateAudience { get; set; } = false;
}