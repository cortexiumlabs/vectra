using Vectra.Infrastructure.SecretManagement;

namespace Vectra.Infrastructure.Caches;

public class SecretManagementService : ISecretManagementService
{
    public ISecretProvider? Current { get; }

    public SecretManagementService(ISecretProviderFactory factory)
    {
        Current = factory.Create();
    }
}