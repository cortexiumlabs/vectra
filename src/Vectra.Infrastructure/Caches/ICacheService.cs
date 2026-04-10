using Vectra.Application.Abstractions.Caches;

namespace Vectra.Infrastructure.Caches;

public interface ICacheService
{
    ICacheProvider Current { get; }
}