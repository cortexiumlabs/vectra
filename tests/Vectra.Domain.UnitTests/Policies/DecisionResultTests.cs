using FluentAssertions;
using Vectra.Domain.Policies;

namespace Vectra.Domain.UnitTests.Policies;

public class DecisionResultTests
{
    [Fact]
    public void Allow_ShouldReturnAllowedDecisionWithDefaultTrustScore()
    {
        var result = DecisionResult.Allow();

        result.IsAllowed.Should().BeTrue();
        result.IsDenied.Should().BeFalse();
        result.IsHitl.Should().BeFalse();
        result.TrustScore.Should().Be(1.0);
        result.Reason.Should().BeNull();
    }

    [Fact]
    public void Allow_ShouldReturnAllowedDecisionWithCustomTrustScore()
    {
        var result = DecisionResult.Allow(0.9);

        result.IsAllowed.Should().BeTrue();
        result.TrustScore.Should().Be(0.9);
    }

    [Fact]
    public void Deny_ShouldReturnDeniedDecisionWithReasonAndDefaultTrustScore()
    {
        var result = DecisionResult.Deny("policy violation");

        result.IsDenied.Should().BeTrue();
        result.IsAllowed.Should().BeFalse();
        result.IsHitl.Should().BeFalse();
        result.Reason.Should().Be("policy violation");
        result.TrustScore.Should().Be(0.0);
    }

    [Fact]
    public void Deny_ShouldReturnDeniedDecisionWithCustomTrustScore()
    {
        var result = DecisionResult.Deny("blocked", 0.2);

        result.IsDenied.Should().BeTrue();
        result.TrustScore.Should().Be(0.2);
    }

    [Fact]
    public void Hitl_ShouldReturnHitlDecisionWithReasonAndDefaultTrustScore()
    {
        var result = DecisionResult.Hitl("needs review");

        result.IsHitl.Should().BeTrue();
        result.IsAllowed.Should().BeFalse();
        result.IsDenied.Should().BeFalse();
        result.Reason.Should().Be("needs review");
        result.TrustScore.Should().Be(0.5);
    }

    [Fact]
    public void Hitl_ShouldReturnHitlDecisionWithCustomTrustScore()
    {
        var result = DecisionResult.Hitl("needs review", 0.4);

        result.IsHitl.Should().BeTrue();
        result.TrustScore.Should().Be(0.4);
    }

    [Theory]
    [InlineData(DecisionType.Allow)]
    [InlineData(DecisionType.Deny)]
    [InlineData(DecisionType.Hitl)]
    public void Type_ShouldMatchDecisionType(DecisionType decisionType)
    {
        DecisionResult result = decisionType switch
        {
            DecisionType.Allow => DecisionResult.Allow(),
            DecisionType.Deny => DecisionResult.Deny("reason"),
            DecisionType.Hitl => DecisionResult.Hitl("reason"),
            _ => throw new ArgumentOutOfRangeException()
        };

        result.Type.Should().Be(decisionType);
    }

    [Fact]
    public void Allow_ShouldBeValueEqualToAnotherAllowWithSameTrustScore()
    {
        var result1 = DecisionResult.Allow(0.8);
        var result2 = DecisionResult.Allow(0.8);

        result1.Should().Be(result2);
    }
}
