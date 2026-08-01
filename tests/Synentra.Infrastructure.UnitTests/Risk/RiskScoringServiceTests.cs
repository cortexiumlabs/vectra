using FluentAssertions;
using NSubstitute;
using Synentra.Application.Abstractions.Caches;
using Synentra.Application.Abstractions.Persistence;
using Synentra.Application.Models;
using Synentra.Infrastructure.Caches;
using Synentra.Infrastructure.Risk;
using Microsoft.Extensions.Logging;

namespace Synentra.Infrastructure.UnitTests.Risk;

public class RiskScoringServiceTests
{
    private readonly RiskScoreAggregator _aggregator;
    private readonly IAgentHistoryRepository _historyRepo = Substitute.For<IAgentHistoryRepository>();
    private readonly ICacheService _cacheService = Substitute.For<ICacheService>();
    private readonly ICacheProvider _cacheProvider = Substitute.For<ICacheProvider>();
    private readonly ILogger<RiskScoringService> _logger = Substitute.For<ILogger<RiskScoringService>>();
    private readonly RiskScoringService _sut;

    public RiskScoringServiceTests()
    {
        _cacheService.Current.Returns(_cacheProvider);

        var calc = Substitute.For<IRiskCalculator>();
        calc.Name.Returns("test");
        calc.Weight.Returns(1.0);
        calc.CalculateAsync(Arg.Any<RiskEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(RiskCalculatorResult.Create("test", 0.4, 1.0));
        _aggregator = new RiskScoreAggregator([calc]);

        _sut = new RiskScoringService(_aggregator, _historyRepo, _cacheService, _logger);
    }

    private static RiskEvaluationContext BuildContext(Guid agentId)
        => new()
        {
            RequestContext = new RequestContext
            {
                AgentId = agentId,
                Method = "GET",
                Path = "/api/data",
                TrustScore = 0.8
            },
            Intent = new IntentClassificationResult
            {
                Label = "suspicious",
                Confidence = 0,
                Status = IntentClassificationStatus.Unavailable
            }
        };

    [Fact]
    public async Task ComputeRiskScoreAsync_CacheHit_ReturnsCachedResult()
    {
        var agentId = Guid.NewGuid();
        var context = BuildContext(agentId);
        var cached = new RiskEvaluationResult
        {
            RiskScore = 0.99,
            TrustScore = 0.8,
            RiskLevel = "high"
        };
        _cacheProvider.TryGetValueAsync<RiskEvaluationResult>(Arg.Any<string>()).Returns((true, cached));

        var result = await _sut.ComputeRiskScoreAsync(context, TestContext.Current.CancellationToken);

        result.RiskScore.Should().Be(0.99);
        await _historyRepo.DidNotReceive().GetRecentAsync(Arg.Any<Guid>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ComputeRiskScoreAsync_CacheMiss_ComputesAndCachesResult()
    {
        var agentId = Guid.NewGuid();
        var context = BuildContext(agentId);
        _cacheProvider.TryGetValueAsync<RiskEvaluationResult>(Arg.Any<string>()).Returns((false, null));

        var result = await _sut.ComputeRiskScoreAsync(context, TestContext.Current.CancellationToken);

        result.RiskScore.Should().BeApproximately(0.4, 1e-9);
        await _cacheProvider.Received(1).SetAsync(Arg.Any<string>(), Arg.Any<RiskEvaluationResult>());
    }

    [Fact]
    public void Constructor_NullAggregator_ThrowsArgumentNullException()
    {
        var act = () => new RiskScoringService(null!, _historyRepo, _cacheService, _logger);

        act.Should().Throw<ArgumentNullException>().WithParameterName("aggregator");
    }

    [Fact]
    public void Constructor_NullHistoryRepo_ThrowsArgumentNullException()
    {
        var act = () => new RiskScoringService(_aggregator, null!, _cacheService, _logger);

        act.Should().Throw<ArgumentNullException>().WithParameterName("historyRepo");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act = () => new RiskScoringService(_aggregator, _historyRepo, _cacheService, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }
}
