namespace Vectra.Infrastructure.SecretManagement;

public interface ISecretManagementService
{
    ISecretProvider? Current { get; }
}