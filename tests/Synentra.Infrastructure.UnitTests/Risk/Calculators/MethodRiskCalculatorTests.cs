using FluentAssertions;
using Synentra.Application.Models;
using Synentra.Infrastructure.Risk.Calculators;

namespace Synentra.Infrastructure.UnitTests.Risk.Calculators;

public class MethodRiskCalculatorTests
{
    private readonly MethodRiskCalculator _sut = new();

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

    [Theory]
    [InlineData("GET", 0.1)]
    [InlineData("HEAD", 0.05)]
    [InlineData("OPTIONS", 0.05)]
    [InlineData("POST", 0.4)]
    [InlineData("PUT", 0.6)]
    [InlineData("PATCH", 0.5)]
    [InlineData("DELETE", 0.9)]
    [InlineData("TRACE", 0.7)]
    [InlineData("CONNECT", 0.8)]
    public async Task CalculateAsync_KnownMethod_ReturnsExpectedRisk(string method, double expectedRisk)
    {
        var requestContext = new RequestContext { Method = method };

        var result = await _sut.CalculateAsync(BuildContext(requestContext), CancellationToken.None);

        result.Score.Should().Be(expectedRisk);
    }

    [Fact]
    public async Task CalculateAsync_UnknownMethod_ReturnsDefaultRisk()
    {
        var requestContext = new RequestContext { Method = "CUSTOM" };

        var result = await _sut.CalculateAsync(BuildContext(requestContext), CancellationToken.None);

        result.Score.Should().Be(0.5);
    }

    [Fact]
    public async Task CalculateAsync_CaseInsensitive_MatchesLowercase()
    {
        var requestContext = new RequestContext { Method = "delete" };

        var result = await _sut.CalculateAsync(BuildContext(requestContext), CancellationToken.None);

        result.Score.Should().Be(0.9);
    }

    [Fact]
    public void Name_ShouldBe_Method()
    {
        _sut.Name.Should().Be("MethodRisk");
    }

    [Fact]
    public void Weight_ShouldBe_0Point15()
    {
        _sut.Weight.Should().Be(0.15);
    }
}
