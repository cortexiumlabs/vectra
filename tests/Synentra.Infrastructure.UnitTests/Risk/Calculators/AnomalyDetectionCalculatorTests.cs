using FluentAssertions;
using NSubstitute;
using Synentra.Application.Models;
using Synentra.Infrastructure.Risk.Calculators;
using Synentra.Infrastructure.Semantic;

namespace Synentra.Infrastructure.UnitTests.Risk.Calculators;

public class AnomalyDetectionCalculatorTests
{
    private readonly IAnomalyDetector _anomalyDetector = Substitute.For<IAnomalyDetector>();
    private readonly AnomalyDetectionCalculator _sut;

    public AnomalyDetectionCalculatorTests()
    {
        _sut = new AnomalyDetectionCalculator(_anomalyDetector);
    }

    private static RiskEvaluationContext BuildContext(RequestContext requestContext)
        => new()
        {
            RequestContext = requestContext,
            Intent = new IntentClassificationResult
            {
                Label = "suspicious",
                Confidence = 0,
                Status = IntentClassificationStatus.Unavailable
            }
        };

    [Fact]
    public async Task CalculateAsync_DelegatesToAnomalyDetector()
    {
        var requestContext = new RequestContext { AgentId = Guid.NewGuid() };
        _anomalyDetector.DetectAsync(requestContext, Arg.Any<CancellationToken>()).Returns(0.75);

        var result = await _sut.CalculateAsync(BuildContext(requestContext), CancellationToken.None);

        result.Score.Should().Be(0.75);
        await _anomalyDetector.Received(1).DetectAsync(requestContext, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CalculateAsync_ZeroAnomalyScore_ReturnsZero()
    {
        var requestContext = new RequestContext();
        _anomalyDetector.DetectAsync(requestContext, Arg.Any<CancellationToken>()).Returns(0.0);

        var result = await _sut.CalculateAsync(BuildContext(requestContext), CancellationToken.None);

        result.Score.Should().Be(0.0);
    }

    [Fact]
    public async Task CalculateAsync_PassesCancellationToken()
    {
        var requestContext = new RequestContext();
        var cts = new CancellationTokenSource();
        _anomalyDetector.DetectAsync(requestContext, cts.Token).Returns(0.5);

        await _sut.CalculateAsync(BuildContext(requestContext), cts.Token);

        await _anomalyDetector.Received(1).DetectAsync(requestContext, cts.Token);
    }

    [Fact]
    public void Name_ShouldBe_Anomaly()
    {
        _sut.Name.Should().Be("AnomalyRisk");
    }

    [Fact]
    public void Weight_ShouldBe_0Point15()
    {
        _sut.Weight.Should().Be(0.15);
    }

    [Fact]
    public async Task CalculateAsync_ReturnsDetectedScore()
    {
        var requestContext = new RequestContext();
        _anomalyDetector.DetectAsync(requestContext, Arg.Any<CancellationToken>()).Returns(0.3);

        var result = await _sut.CalculateAsync(BuildContext(requestContext), CancellationToken.None);

        result.Score.Should().Be(0.3);
    }
}
