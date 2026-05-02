namespace Vectra.BuildingBlocks.Configuration.SecretManagement;

public class SecretManagementConfiguration
{
    public SecretManagementProviderType DefaultProvider { get; set; } = SecretManagementProviderType.None;
    public SecretManagementProviders Providers { get; set; } = new();
}