using Microsoft.Extensions.Configuration;
using Vectra.BuildingBlocks.Configuration.SecretManagement;

namespace Vectra.Infrastructure.SecretManagement.Providers;

internal sealed class EnvironmentVariablesSecretProvider : ISecretProvider
{
    private readonly EnvironmentVariablesSecretConfiguration _config;

    public EnvironmentVariablesSecretProvider(
        EnvironmentVariablesSecretConfiguration config)
    {
        _config = config;
    }

    public void Configure(IConfigurationBuilder builder)
    {
        var prefix = string.IsNullOrWhiteSpace(_config.Prefix) ? null : _config.Prefix;
        builder.AddEnvironmentVariables(prefix);
    }
}
