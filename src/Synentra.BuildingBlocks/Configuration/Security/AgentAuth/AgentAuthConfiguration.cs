namespace Synentra.BuildingBlocks.Configuration.Security.AgentAuth;

public class AgentAuthConfiguration
{
    public bool UseCustomHeader { get; set; } = true;
    public string CustomHeaderName { get; set; } = "Synentra-Authorization";
    public bool FallbackToAuthorization { get; set; } = false;
    public TokenIssuanceConfiguration TokenIssuance { get; set; } = new();
    public ExternalIdentityConfiguration ExternalIdentity { get; set; } = new();
}