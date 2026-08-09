using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synentra.Application.Abstractions.Caches;
using Synentra.Application.Abstractions.Executions;
using Synentra.Application.Abstractions.Persistence;
using Synentra.Application.Models;
using Synentra.BuildingBlocks.Configuration.Risk;
using Synentra.Infrastructure.Caches;

namespace Synentra.Infrastructure.Risk;

public class RiskScoringService : IRiskScoringService
{
    private readonly RiskScoreAggregator _aggregator;
    private readonly IAgentHistoryRepository? _historyRepo;
    private readonly ICacheProvider _cacheProvider;
    private readonly RiskConfiguration _configuration;
    private readonly ILogger<RiskScoringService> _logger;

    public RiskScoringService(
        RiskScoreAggregator aggregator,
        ICacheService cacheService,
        ILogger<RiskScoringService> logger)
    {
        _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
        _configuration = new RiskConfiguration();
        _cacheProvider = cacheService?.Current ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public RiskScoringService(
        RiskScoreAggregator aggregator,
        IAgentHistoryRepository historyRepo,
        IOptions<RiskConfiguration> configuration,
        ICacheService cacheService,
        ILogger<RiskScoringService> logger)
        : this(aggregator, cacheService, logger)
    {
        _ = historyRepo ?? throw new ArgumentNullException(nameof(historyRepo));
        _ = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _historyRepo = historyRepo;
        _configuration = configuration.Value;
    }

    public RiskScoringService(
        RiskScoreAggregator aggregator,
        IAgentHistoryRepository historyRepo,
        ICacheService cacheService,
        ILogger<RiskScoringService> logger)
        : this(
            aggregator,
            historyRepo,
            Options.Create(new RiskConfiguration()),
            cacheService,
            logger)
    {
    }

    public async Task<RiskEvaluationResult> ComputeRiskScoreAsync(RiskEvaluationContext context, CancellationToken cancellationToken = default)
    {
        var requestContext = context.RequestContext;

        if (_configuration.Enabled == false)
        {
            return new RiskEvaluationResult
            {
                RiskScore = 0,
                TrustScore = Math.Clamp(requestContext.TrustScore, 0, 1),
                RiskLevel = "low"
            };
        }

        // Build a cache key based on agent ID and request fingerprint
        var cacheKey = $"risk:{requestContext.AgentId}:{requestContext.Method}:{requestContext.Path}:{context.Intent.Label}:{DateTime.UtcNow:yyyyMMddHHmm}";
        var (success, cachedResult) = await _cacheProvider.TryGetValueAsync<RiskEvaluationResult>(cacheKey);

        if (success && cachedResult is not null)
            return cachedResult;

        var calculators = await _aggregator.AggregateAsync(context, cancellationToken);

        var totalWeight = calculators.Sum(x => x.Weight);
        var weightedSum = calculators.Sum(x => x.Score * x.Weight);

        const double epsilon = 1e-9;
        var score = Math.Abs(totalWeight) < epsilon
            ? 0
            : Math.Clamp(weightedSum / totalWeight, 0, 1);

        var trustScore = Math.Clamp(requestContext.TrustScore, 0, 1);
        var riskLevel = score switch
        {
            >= 0.85 => "critical",
            >= 0.70 => "high",
            >= 0.40 => "moderate",
            _ => "low"
        };

        var signals = calculators.SelectMany(x => x.Signals).ToArray();
        var result = new RiskEvaluationResult
        {
            RiskScore = score,
            TrustScore = trustScore,
            RiskLevel = riskLevel,
            Calculators = calculators,
            Signals = signals
        };

        // Cache for a short period (e.g., 10 seconds) to avoid over‑calculation for same agent in burst
        await _cacheProvider.SetAsync(cacheKey, result);
        _logger.LogDebug("Risk score for agent {AgentId}: {Score}", requestContext.AgentId, score);
        return result;
    }
}