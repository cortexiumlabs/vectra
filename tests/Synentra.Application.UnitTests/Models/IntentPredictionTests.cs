using FluentAssertions;
using Synentra.Application.Models;

namespace Synentra.Application.UnitTests.Models;

public class IntentPredictionTests
{
    [Fact]
    public void Create_WithLabelAndConfidence_ShouldSetProperties()
    {
        var pred = new IntentPrediction { Label = "greet", Confidence = 0.95 };

        pred.Label.Should().Be("greet");
        pred.Confidence.Should().Be(0.95);
    }

    [Fact]
    public void TwoPredictionsWithSameValues_ShouldBeEqual()
    {
        var a = new IntentPrediction { Label = "x", Confidence = 0.5 };
        var b = new IntentPrediction { Label = "x", Confidence = 0.5 };

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        var original = new IntentPrediction { Label = "hello", Confidence = 0.1 };
        var modified = original with { Confidence = 0.9 };

        modified.Should().NotBeSameAs(original);
        modified.Label.Should().Be(original.Label);
        modified.Confidence.Should().Be(0.9);
        original.Confidence.Should().Be(0.1);
    }

    [Fact]
    public void ToString_ShouldContainProperties()
    {
        var p = new IntentPrediction { Label = "abc", Confidence = 0.123 };

        var text = p.ToString();

        text.Should().Contain("abc");
        (text.Contains("0.123") || text.Contains("0.12300000000000001")).Should().BeTrue();
    }
}
