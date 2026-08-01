using FluentAssertions;
using Synentra.Application.Models;
using Synentra.Infrastructure.Risk.Calculators;

namespace Synentra.Infrastructure.UnitTests.Risk.Calculators;

public class PathRiskCalculatorTests
{
    private readonly PathRiskCalculator _sut = new();

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
    [InlineData("/admin/users", 0.8)]
    [InlineData("/api/export", 0.9)]
    [InlineData("/api/dump", 0.9)]
    [InlineData("/api/bulk", 0.9)]
    [InlineData("/api/delete/item", 0.85)]
    [InlineData("/api/remove/item", 0.85)]
    [InlineData("/api/drop/table", 0.85)]
    [InlineData("/users/all", 0.95)]
    [InlineData("/users/export", 0.95)]
    [InlineData("/api/config", 0.7)]
    [InlineData("/api/settings", 0.7)]
    [InlineData("/api/env", 0.7)]
    [InlineData("/internal/service", 0.6)]
    [InlineData("/v1/endpoint", 0.2)]
    [InlineData("/api/data", 0.1)]
    public async Task CalculateAsync_Path_ReturnsExpectedRisk(string path, double expectedRisk)
    {
        var requestContext = new RequestContext { Path = path };

        var result = await _sut.CalculateAsync(BuildContext(requestContext), CancellationToken.None);

        result.Score.Should().Be(expectedRisk);
    }

    [Fact]
    public async Task CalculateAsync_PathWithMultiplePatterns_ReturnsHighestRisk()
    {
        // /users/export matches both /export (0.9) and /users/export (0.95)
        var requestContext = new RequestContext { Path = "/users/export" };

        var result = await _sut.CalculateAsync(BuildContext(requestContext), CancellationToken.None);

        result.Score.Should().Be(0.95);
    }

    [Fact]
    public async Task CalculateAsync_UnknownPath_ReturnsDefaultLowRisk()
    {
        var requestContext = new RequestContext { Path = "/api/health" };

        var result = await _sut.CalculateAsync(BuildContext(requestContext), CancellationToken.None);

        result.Score.Should().Be(0.1);
    }

    [Fact]
    public async Task CalculateAsync_CaseInsensitive_MatchesUppercase()
    {
        var requestContext = new RequestContext { Path = "/ADMIN/users" };

        var result = await _sut.CalculateAsync(BuildContext(requestContext), CancellationToken.None);

        result.Score.Should().Be(0.8);
    }

    [Fact]
    public void Name_ShouldBe_Path()
    {
        _sut.Name.Should().Be("PathRisk");
    }

    [Fact]
    public void Weight_ShouldBe_0Point20()
    {
        _sut.Weight.Should().Be(0.20);
    }
}
