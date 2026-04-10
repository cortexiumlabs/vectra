namespace Vectra.BuildingBlocks.Configuration.Security.AgentAuth;

public enum JwtProviderType
{
    SelfSigned,
    Keycloak,
    Auth0,
    AzureAd,
    Custom
}