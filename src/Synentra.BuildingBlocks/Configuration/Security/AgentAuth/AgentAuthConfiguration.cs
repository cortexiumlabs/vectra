namespace Synentra.BuildingBlocks.Configuration.Security.AgentAuth;

public class AgentAuthConfiguration
{
    public AgentAuthProviderType Provider { get; set; } = AgentAuthProviderType.SelfSigned;
    public SelfSignedProvider SelfSigned { get; set; } = new();
    public JwtProvider Jwt { get; set; } = new();
    public bool UseCustomHeader { get; set; } = true;
    public string CustomHeaderName { get; set; } = "Synentra-Authorization";
    public bool FallbackToAuthorization { get; set; } = false;
}