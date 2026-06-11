using Synentra.Infrastructure.SecretManagement;

namespace Synentra.Infrastructure.Caches;

public class SecretManagementService : ISecretManagementService
{
    public ISecretProvider? Current { get; }

    public SecretManagementService(ISecretProviderFactory factory)
    {
        Current = factory.Create();
    }
}