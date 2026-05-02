namespace Vectra.Infrastructure.SecretManagement;

public interface ISecretProviderFactory
{
    ISecretProvider? Create();
}
