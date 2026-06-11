using Synentra.Application.Abstractions.Caches;

namespace Synentra.Infrastructure.Caches;

public interface ICacheService
{
    ICacheProvider Current { get; }
}