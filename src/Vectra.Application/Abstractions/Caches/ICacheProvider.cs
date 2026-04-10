namespace Vectra.Application.Abstractions.Caches;

public interface ICacheProvider
{
    Task SetAsync(string key, string value);
    Task<string?> GetAsync(string key);
}