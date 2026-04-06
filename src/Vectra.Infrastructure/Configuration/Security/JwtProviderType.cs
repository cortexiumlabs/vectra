namespace Vectra.Infrastructure.Configuration.Security;

public enum JwtProviderType
{
    SelfSigned,
    Keycloak,
    Auth0,
    AzureAd,
    Custom
}