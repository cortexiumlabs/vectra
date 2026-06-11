namespace Synentra.BuildingBlocks.Configuration.SecretManagement;

public enum SecretManagementProviderType
{
    None,
    EnvironmentVariables,
    AzureKeyVault,
    UserSecrets,
}