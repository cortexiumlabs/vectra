namespace Synentra.Infrastructure.SecretManagement;

public interface ISecretManagementService
{
    ISecretProvider? Current { get; }
}