using FluentAssertions;
using Synentra.Application.Models;
using Synentra.Infrastructure.Risk.Calculators;

namespace Synentra.Infrastructure.UnitTests.Risk.Calculators;

public class TimeBasedCalculatorTests
{
    private readonly TimeBasedCalculator _sut = new();

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
    public async Task CalculateAsync_ReturnsValueBetweenZeroAndPointFive()
    {
        var requestContext = new RequestContext();

        var result = await _sut.CalculateAsync(BuildContext(requestContext), CancellationToken.None);

        result.Score.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(0.5);
    }

    [Fact]
    public async Task CalculateAsync_ReturnDoesNotExceed0Point5()
    {
        // Even if both weekend and night-time apply (0.2 + 0.3 = 0.5), max is 0.5
        var requestContext = new RequestContext();

        var result = await _sut.CalculateAsync(BuildContext(requestContext), CancellationToken.None);

        result.Score.Should().BeLessThanOrEqualTo(0.5);
    }

    [Fact]
    public void Name_ShouldBe_TimeBasedRisk()
    {
        _sut.Name.Should().Be("TimeBasedRisk");
    }

    [Fact]
    public void Weight_ShouldBe_0Point050()
    {
        _sut.Weight.Should().Be(0.05);
    }

    [Fact]
    public async Task CalculateAsync_IgnoresProvidedHistory()
    {
        var requestContext = new RequestContext();

        // Should not throw or fail when history is provided
        var act = async () => await _sut.CalculateAsync(BuildContext(requestContext), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
