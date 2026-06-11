using Microsoft.Extensions.Configuration;

namespace Vectra.Infrastructure.SecretManagement;

public interface ISecretProvider
{
    void Configure(IConfigurationBuilder builder);
}
