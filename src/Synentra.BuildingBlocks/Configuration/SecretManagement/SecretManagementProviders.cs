namespace Synentra.BuildingBlocks.Configuration.SecretManagement;

public class SecretManagementProviders
{
    public EnvironmentVariablesSecretConfiguration EnvironmentVariables { get; set; } = new();
    public AzureKeyVaultSecretConfiguration AzureKeyVault { get; set; } = new();
}