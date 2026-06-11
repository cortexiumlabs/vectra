using Microsoft.Extensions.Configuration;

namespace Synentra.Infrastructure.SecretManagement.Providers;

internal sealed class UserSecretsSecretProvider : ISecretProvider
{
    public void Configure(IConfigurationBuilder builder)
    {
        var assembly = System.Reflection.Assembly.GetEntryAssembly()
            ?? typeof(UserSecretsSecretProvider).Assembly;

        builder.AddUserSecrets(assembly, optional: true);
    }
}
