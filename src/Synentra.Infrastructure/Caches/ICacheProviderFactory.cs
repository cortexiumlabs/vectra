using Vectra.Application.Abstractions.Caches;

namespace Vectra.Infrastructure.Caches;

public interface ICacheProviderFactory
{
    ICacheProvider Create();
}