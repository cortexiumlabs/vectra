using Microsoft.Extensions.Configuration;

namespace Synentra.Infrastructure.SecretManagement;

public interface ISecretProvider
{
    void Configure(IConfigurationBuilder builder);
}
