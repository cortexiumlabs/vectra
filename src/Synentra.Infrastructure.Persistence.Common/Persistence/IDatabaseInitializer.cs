namespace Vectra.Infrastructure.Persistence.Common;

public interface IDatabaseInitializer
{
    Task EnsureDatabaseCreatedAsync(CancellationToken cancellationToken = default);
}