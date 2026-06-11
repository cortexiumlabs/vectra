using Synentra.Application.Abstractions.Caches;

namespace Synentra.Infrastructure.Caches;

public interface ICacheProviderFactory
{
    ICacheProvider Create();
}