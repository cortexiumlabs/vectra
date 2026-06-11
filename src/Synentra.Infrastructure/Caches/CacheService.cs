using Synentra.Application.Abstractions.Caches;

namespace Synentra.Infrastructure.Caches;

public class CacheService : ICacheService
{
    public ICacheProvider Current { get; }

    public CacheService(ICacheProviderFactory factory)
    {
        Current = factory.Create();
    }
}