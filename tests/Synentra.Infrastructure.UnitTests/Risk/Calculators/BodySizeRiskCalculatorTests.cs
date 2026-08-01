using FluentAssertions;
using Synentra.Application.Models;
using Synentra.Infrastructure.Risk.Calculators;

namespace Synentra.Infrastructure.UnitTests.Risk.Calculators;

public class BodySizeRiskCalculatorTests
{
    private readonly BodySizeRiskCalculator _sut = new();

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
    public async Task CalculateAsync_NullBody_ReturnsZero()
    {
        var requestContext = new RequestContext { Body = null };

        var result = await _sut.CalculateAsync(BuildContext(requestContext), CancellationToken.None);

        result.Score.Should().Be(0.0);
    }

    [Fact]
    public async Task CalculateAsync_EmptyBody_ReturnsZero()
    {
        var requestContext = new RequestContext { Body = string.Empty };

        var result = await _sut.CalculateAsync(BuildContext(requestContext), CancellationToken.None);

        result.Score.Should().Be(0.0);
    }

    [Fact]
    public async Task CalculateAsync_SmallBody_ReturnsZero()
    {
        var requestContext = new RequestContext { Body = new string('x', 5 * 1024) }; // 5 KB

        var result = await _sut.CalculateAsync(BuildContext(requestContext), CancellationToken.None);

        result.Score.Should().Be(0.0);
    }

    [Fact]
    public async Task CalculateAsync_BodyOver10KB_Returns0Point2()
    {
        var requestContext = new RequestContext { Body = new string('x', 20 * 1024) }; // 20 KB

        var result = await _sut.CalculateAsync(BuildContext(requestContext), CancellationToken.None);

        result.Score.Should().Be(0.2);
    }

    [Fact]
    public async Task CalculateAsync_BodyOver100KB_Returns0Point5()
    {
        var requestContext = new RequestContext { Body = new string('x', 200 * 1024) }; // 200 KB

        var result = await _sut.CalculateAsync(BuildContext(requestContext), CancellationToken.None);

        result.Score.Should().Be(0.5);
    }

    [Fact]
    public async Task CalculateAsync_BodyOver1MB_Returns0Point8()
    {
        var requestContext = new RequestContext { Body = new string('x', 2 * 1024 * 1024) }; // 2 MB

        var result = await _sut.CalculateAsync(BuildContext(requestContext), CancellationToken.None);

        result.Score.Should().Be(0.8);
    }

    [Fact]
    public void Name_ShouldBe_BodySize()
    {
        _sut.Name.Should().Be("BodySizeRisk");
    }

    [Fact]
    public void Weight_ShouldBe_0Point050()
    {
        _sut.Weight.Should().Be(0.05);
    }
}
