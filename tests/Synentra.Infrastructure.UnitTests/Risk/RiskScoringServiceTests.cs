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

    [Fact]
    public async Task ComputeRiskScoreAsync_DisabledConfiguration_ReturnsZeroRiskAndClampedTrust()
    {
        var cfg = Microsoft.Extensions.Options.Options.Create(new Synentra.BuildingBlocks.Configuration.Risk.RiskConfiguration { Enabled = false });
        var sut = new RiskScoringService(_aggregator, _historyRepo, cfg, _cacheService, _logger);

        var agentId = Guid.NewGuid();
        var ctx = BuildContext(agentId);
        ctx.RequestContext.TrustScore = 2.5; // should be clamped to 1

        var result = await sut.ComputeRiskScoreAsync(ctx, TestContext.Current.CancellationToken);

        result.RiskScore.Should().Be(0);
        result.TrustScore.Should().Be(1);
        result.RiskLevel.Should().Be("low");
    }

    [Fact]
    public async Task ComputeRiskScoreAsync_TotalWeightZero_ResultsInZeroScore()
    {
        var calc = Substitute.For<IRiskCalculator>();
        calc.Name.Returns("zero");
        calc.Weight.Returns(0.0);
        calc.CalculateAsync(Arg.Any<RiskEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(RiskCalculatorResult.Create("zero", 0.4, 0.0));

        var aggregator = new RiskScoreAggregator(new[] { calc });
        var sut = new RiskScoringService(aggregator, _historyRepo, _cacheService, _logger);

        var ctx = BuildContext(Guid.NewGuid());
        _cacheProvider.TryGetValueAsync<RiskEvaluationResult>(Arg.Any<string>()).Returns((false, null));

        var result = await sut.ComputeRiskScoreAsync(ctx, TestContext.Current.CancellationToken);

        result.RiskScore.Should().Be(0);
        result.RiskLevel.Should().Be("low");
    }

    [Theory]
    [InlineData(0.39, "low")]
    [InlineData(0.4, "moderate")]
    [InlineData(0.7, "high")]
    [InlineData(0.85, "critical")]
    public async Task ComputeRiskScoreAsync_RiskLevelBoundaries_MapCorrectly(double score, string expectedLevel)
    {
        var calc = Substitute.For<IRiskCalculator>();
        calc.Name.Returns("bnd");
        calc.Weight.Returns(1.0);
        calc.CalculateAsync(Arg.Any<RiskEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(RiskCalculatorResult.Create("bnd", score, 1.0));

        var aggregator = new RiskScoreAggregator(new[] { calc });
        var sut = new RiskScoringService(aggregator, _historyRepo, _cacheService, _logger);

        var ctx = BuildContext(Guid.NewGuid());
        _cacheProvider.TryGetValueAsync<RiskEvaluationResult>(Arg.Any<string>()).Returns((false, null));

        var result = await sut.ComputeRiskScoreAsync(ctx, TestContext.Current.CancellationToken);

        result.RiskLevel.Should().Be(expectedLevel);
    }

    [Fact]
    public async Task ComputeRiskScoreAsync_TrustScoreClamped_NegativeAndAboveOne()
    {
        var sut = new RiskScoringService(_aggregator, _historyRepo, _cacheService, _logger);

        var ctxLow = BuildContext(Guid.NewGuid());
        ctxLow.RequestContext.TrustScore = -5;
        _cacheProvider.TryGetValueAsync<RiskEvaluationResult>(Arg.Any<string>()).Returns((false, null));
        var resLow = await sut.ComputeRiskScoreAsync(ctxLow, TestContext.Current.CancellationToken);
        resLow.TrustScore.Should().Be(0);

        var ctxHigh = BuildContext(Guid.NewGuid());
        ctxHigh.RequestContext.TrustScore = 5;
        _cacheProvider.TryGetValueAsync<RiskEvaluationResult>(Arg.Any<string>()).Returns((false, null));
        var resHigh = await sut.ComputeRiskScoreAsync(ctxHigh, TestContext.Current.CancellationToken);
        resHigh.TrustScore.Should().Be(1);
    }

    [Fact]
    public void Constructor_FirstOverload_NullCacheService_Throws()
    {
        var act = () => new RiskScoringService(_aggregator, null!, _logger);
        act.Should().Throw<ArgumentNullException>();
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
