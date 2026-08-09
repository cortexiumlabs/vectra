using FluentAssertions;
using Synentra.Application.Models;
using Synentra.Infrastructure.Risk.Calculators;

namespace Synentra.Infrastructure.UnitTests.Risk.Calculators;

public class IntentRiskCalculatorTests
{
    private static RiskEvaluationContext BuildContext(string label, double confidence, IntentClassificationStatus status)
    {
        return new RiskEvaluationContext
        {
            RequestContext = new RequestContext { Method = "GET", Path = "/", AgentId = Guid.NewGuid() },
            Intent = new IntentClassificationResult { Label = label, Confidence = confidence, Status = status }
        };
    }

    [Fact]
    public async Task NameAndWeight_AreCorrect()
    {
        var c = new IntentRiskCalculator();
        c.Name.Should().Be("IntentRisk");
        c.Weight.Should().Be(0.25);
    }

    [Fact]
    public async Task CalculateAsync_KnownLabel_Classified_AdjustsCorrectly()
    {
        var sut = new IntentRiskCalculator();
        var ctx = BuildContext("health_check", 0.5, IntentClassificationStatus.Classified);

        var res = await sut.CalculateAsync(ctx, CancellationToken.None);

        res.Name.Should().Be("IntentRisk");
        res.Weight.Should().Be(0.25);
        // base 0.05 + classified adjustment 0.0
        res.Score.Should().BeApproximately(0.05, 1e-9);
        res.Signals.Should().ContainSingle();
        var sig = res.Signals.Single();
        sig.Code.Should().Be("intent_risk");
        sig.Description.Should().Contain("health_check");
        sig.Description.Should().Contain("0.50");
    }

    [Fact]
    public async Task CalculateAsync_MixedCaseLabel_IsCaseInsensitive()
    {
        var sut = new IntentRiskCalculator();
        var ctx = BuildContext("HeAlTh_ChEcK", 0.123, IntentClassificationStatus.Classified);

        var res = await sut.CalculateAsync(ctx, CancellationToken.None);

        res.Score.Should().BeApproximately(0.05, 1e-9);
        res.Signals.Single().Description.Should().Contain("0.12");
    }

    [Fact]
    public async Task CalculateAsync_UnknownLabel_UsesDefaultBase()
    {
        var sut = new IntentRiskCalculator();
        var ctx = BuildContext("not-a-known-intent", 0.4, IntentClassificationStatus.Classified);

        var res = await sut.CalculateAsync(ctx, CancellationToken.None);

        // default base 0.85 + classified adjustment 0.0
        res.Score.Should().BeApproximately(0.85, 1e-9);
    }

    [Theory]
    [InlineData(IntentClassificationStatus.LowConfidence, 0.10)]
    [InlineData(IntentClassificationStatus.Failed, 0.15)]
    [InlineData(IntentClassificationStatus.Unavailable, 0.15)]
    public async Task CalculateAsync_StatusAdjustments_Applied(IntentClassificationStatus status, double adjustment)
    {
        var sut = new IntentRiskCalculator();
        var ctx = BuildContext("list", 0.7, status); // 'list' base 0.10

        var res = await sut.CalculateAsync(ctx, CancellationToken.None);

        var expected = Math.Clamp(0.10 + adjustment, 0.0, 1.0);
        res.Score.Should().BeApproximately(expected, 1e-9);
    }

    [Fact]
    public async Task CalculateAsync_DefaultStatusBranch_UsedForUnknownEnumValue()
    {
        var sut = new IntentRiskCalculator();
        var unknownStatus = (IntentClassificationStatus)999;
        var ctx = BuildContext("create", 0.2, unknownStatus); // base 0.35

        var res = await sut.CalculateAsync(ctx, CancellationToken.None);

        // default adjustment 0.10
        res.Score.Should().BeApproximately(Math.Clamp(0.35 + 0.10, 0.0, 1.0), 1e-9);
    }

    [Fact]
    public async Task CalculateAsync_ScoreClampedAtOne()
    {
        var sut = new IntentRiskCalculator();
        // 'harmful' base 1.00 plus any adjustment should clamp to 1.0
        var ctx = BuildContext("harmful", 0.99, IntentClassificationStatus.Failed);

        var res = await sut.CalculateAsync(ctx, CancellationToken.None);

        res.Score.Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public async Task CalculateAsync_CancellationRequested_Throws()
    {
        var sut = new IntentRiskCalculator();
        var ctx = BuildContext("list", 0.1, IntentClassificationStatus.Classified);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await sut.CalculateAsync(ctx, cts.Token));
    }
}
