namespace Vectra.Core.Interfaces;

public interface IPolicyCacheInvalidator
{
    Task InvalidateAsync(Guid policyId, CancellationToken cancellationToken = default);
}