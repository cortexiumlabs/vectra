using FluentAssertions;
using NSubstitute;
using Synentra.Application.Models;
using Synentra.Infrastructure.Risk;

namespace Synentra.Infrastructure.UnitTests.Risk;

public class RiskScoreAggregatorTests
{
    private static RiskEvaluationContext BuildContext()
        => new()
        {
            RequestContext = new RequestContext
            {
                AgentId = Guid.NewGuid(),
                Method = "GET",
                Path = "/api/data"
            },
            Intent = new IntentClassificationResult
            {
                Label = "suspicious",
                Confidence = 0,
                Status = IntentClassificationStatus.Unavailable
            }
        };

    private static IRiskCalculator CreateCalculator(string name, double weight, double score)
    {
        var calc = Substitute.For<IRiskCalculator>();
        calc.Name.Returns(name);
        calc.Weight.Returns(weight);
        calc.CalculateAsync(Arg.Any<RiskEvaluationContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(RiskCalculatorResult.Create(name, score, weight)));
        return calc;
    }

    [Fact]
    public async Task AggregateAsync_NoCalculators_ReturnsEmpty()
    {
        var sut = new RiskScoreAggregator(Array.Empty<IRiskCalculator>());

        var result = await sut.AggregateAsync(BuildContext(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task AggregateAsync_SingleCalculator_ReturnsSingleResult()
    {
        var calc = CreateCalculator("test", 1.0, 0.7);
        var sut = new RiskScoreAggregator([calc]);

        var result = await sut.AggregateAsync(BuildContext(), CancellationToken.None);

        result.Should().ContainSingle();
        result.Single().Score.Should().BeApproximately(0.7, 1e-9);
    }

    [Fact]
    public async Task AggregateAsync_MultipleCalculators_ReturnsAllResults()
    {
        var calc1 = CreateCalculator("c1", 2.0, 0.5);
        var calc2 = CreateCalculator("c2", 1.0, 0.8);
        var sut = new RiskScoreAggregator([calc1, calc2]);

        var result = await sut.AggregateAsync(BuildContext(), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(x => x.Name).Should().BeEquivalentTo(["c1", "c2"]);
    }

    [Fact]
    public async Task AggregateAsync_UsesConfiguredWeightWhenResultWeightDiffers()
    {
        var calc = CreateCalculator("c1", 1.0, 0.0);
        var sut = new RiskScoreAggregator([calc]);

        var result = await sut.AggregateAsync(BuildContext(), CancellationToken.None);

        result.Single().Weight.Should().Be(1.0);
    }

    [Fact]
    public async Task AggregateAsync_PassesContextToCalculators()
    {
        var context = BuildContext();
        context.RequestContext.Method = "DELETE";
        var calc = CreateCalculator("c1", 1.0, 0.5);
        var sut = new RiskScoreAggregator([calc]);

        await sut.AggregateAsync(context, CancellationToken.None);

        await calc.Received(1).CalculateAsync(
            Arg.Is<RiskEvaluationContext>(x => x.RequestContext == context.RequestContext),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AggregateAsync_AllCalculatorsRunConcurrently()
    {
        var calcs = Enumerable.Range(1, 5)
            .Select(i => CreateCalculator($"c{i}", 1.0, 0.5))
            .ToList();
        var sut = new RiskScoreAggregator(calcs);

        var result = await sut.AggregateAsync(BuildContext(), CancellationToken.None);

        result.Should().HaveCount(5);
        foreach (var calc in calcs)
            await calc.Received(1).CalculateAsync(Arg.Any<RiskEvaluationContext>(), Arg.Any<CancellationToken>());
    }
}
