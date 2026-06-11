using Microsoft.Extensions.Logging;
using Vectra.Application.Abstractions.Caches;
using Vectra.Application.Abstractions.Executions;
using Vectra.Domain.Policies;
using Vectra.Infrastructure.Caches;

namespace Vectra.Infrastructure.Policy;

internal sealed class PolicyCacheService : IPolicyCacheService
{
    private const string CacheKey = "policies:all";

    private readonly IPolicyLoader _policyLoader;
    private readonly ICacheProvider _cacheProvider;
    private readonly ILogger<PolicyCacheService> _logger;

    public PolicyCacheService(
        IPolicyLoader policyLoader,
        ICacheService cacheService,
        ILogger<PolicyCacheService> logger)
    {
        _policyLoader = policyLoader ?? throw new ArgumentNullException(nameof(policyLoader));
        _cacheProvider = cacheService.Current ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<(IReadOnlyList<PolicyDefinition> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (hit, cached) = await _cacheProvider.TryGetValueAsync<Dictionary<string, PolicyDefinition>>(CacheKey);

        Dictionary<string, PolicyDefinition> all;
        if (hit && cached is not null)
        {
            _logger.LogDebug("Policies resolved from cache");
            all = cached;
        }
        else
        {
            _logger.LogDebug("Cache miss — loading policies via IPolicyLoader");
            all = await _policyLoader.LoadAllAsync(cancellationToken);
            await _cacheProvider.SetAsync(CacheKey, all);
        }

        var totalCount = all.Count;
        var items = all.Values
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }
}
