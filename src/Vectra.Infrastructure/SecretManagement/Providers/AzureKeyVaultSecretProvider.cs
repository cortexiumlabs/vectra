using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Vectra.BuildingBlocks.Configuration.SecretManagement;

namespace Vectra.Infrastructure.SecretManagement.Providers;

internal sealed class AzureKeyVaultSecretProvider : ISecretProvider
{
    private readonly AzureKeyVaultSecretConfiguration _config;

    public AzureKeyVaultSecretProvider(
        AzureKeyVaultSecretConfiguration config)
    {
        _config = config;
    }

    public void Configure(IConfigurationBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(_config.VaultUri))
            throw new InvalidOperationException(
                "SecretManagement.AzureKeyVault.VaultUri must be set when the AzureKeyVault provider is selected.");

        var client = new SecretClient(new Uri(_config.VaultUri), new DefaultAzureCredential());
        var manager = string.IsNullOrWhiteSpace(_config.SecretPrefix)
            ? (KeyVaultSecretManager)new KeyVaultSecretManager()
            : new PrefixKeyVaultSecretManager(_config.SecretPrefix);

        var options = new AzureKeyVaultConfigurationOptions { Manager = manager };

        if (_config.ReloadOnChange)
            options.ReloadInterval = _config.ReloadInterval;

        try
        {
            builder.AddAzureKeyVault(client, options);
        }
        catch (Exception) when (_config.Optional)
        {
            // Vault unreachable in optional mode – skip silently.
        }
    }

    private sealed class PrefixKeyVaultSecretManager : KeyVaultSecretManager
    {
        private readonly string _prefix;

        public PrefixKeyVaultSecretManager(string prefix)
            => _prefix = prefix.TrimEnd('-') + "--";

        public override bool Load(SecretProperties secret)
            => secret.Name.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase);

        public override string GetKey(KeyVaultSecret secret)
            => secret.Name[_prefix.Length..].Replace("--", ConfigurationPath.KeyDelimiter);
    }
}
