using Vectra.Application.Abstractions.Caches;

namespace Vectra.Infrastructure.Caches;

public class CacheService : ICacheService
{
    public ICacheProvider Current { get; }

    public CacheService(ICacheProviderFactory factory)
    {
        Current = factory.Create();
    }
}