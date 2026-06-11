using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vectra.BuildingBlocks.Configuration.SecretManagement;
using Vectra.Infrastructure.SecretManagement.Providers;

namespace Vectra.Infrastructure.SecretManagement;

public class SecretProviderFactory : ISecretProviderFactory
{
    private readonly SecretManagementConfiguration _config;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SecretProviderFactory> _logger;

    public SecretProviderFactory(
        IOptions<SecretManagementConfiguration> options,
        IServiceProvider serviceProvider,
        ILogger<SecretProviderFactory> logger)
    {
        _config = options.Value
            ?? throw new InvalidOperationException("Secret management configuration is missing.");

        _serviceProvider = serviceProvider
            ?? throw new ArgumentNullException(nameof(serviceProvider));

        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
    }

    public ISecretProvider? Create()
    {
        return _config.DefaultProvider switch
        {
            SecretManagementProviderType.EnvironmentVariables => CreateEnvironmentVariables(),
            SecretManagementProviderType.AzureKeyVault => CreateAzureKeyVault(),
            SecretManagementProviderType.UserSecrets => CreateUserSecrets(),
            _ => null
        };
    }

    private ISecretProvider CreateEnvironmentVariables()
    {
        _logger.LogInformation("Creating Environment Variables Secret Provider.");
        return ActivatorUtilities.CreateInstance<EnvironmentVariablesSecretProvider>(_serviceProvider, _config.Providers.EnvironmentVariables);
    }
    private ISecretProvider CreateAzureKeyVault()
    {
        _logger.LogInformation("Creating Azure Key Vault Secret Provider.");
        return ActivatorUtilities.CreateInstance<AzureKeyVaultSecretProvider>(_serviceProvider, _config.Providers.AzureKeyVault);
    }

    private ISecretProvider CreateUserSecrets()
    {
        _logger.LogInformation("Creating User Secrets Secret Provider.");
        return ActivatorUtilities.CreateInstance<UserSecretsSecretProvider>(_serviceProvider);
    }
}
