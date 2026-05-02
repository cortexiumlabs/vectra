namespace Vectra.BuildingBlocks.Configuration.SecretManagement;

public class AzureKeyVaultSecretConfiguration
{
    public string? VaultUri { get; set; }
    public bool Optional { get; set; } = false;
    public string? SecretPrefix { get; set; }
    public bool ReloadOnChange { get; set; } = false;
    public TimeSpan ReloadInterval { get; set; } = TimeSpan.FromMinutes(30);
}
