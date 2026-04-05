namespace Vectra.Application.Abstractions.Executions;

public interface IPolicyCacheInvalidator
{
    Task InvalidateAsync(Guid policyId, CancellationToken cancellationToken = default);
}