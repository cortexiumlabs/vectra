namespace Synentra.Infrastructure.SecretManagement;

public interface ISecretProviderFactory
{
    ISecretProvider? Create();
}
