using FluentAssertions;
using Synentra.Application.Models;

namespace Synentra.Application.UnitTests.Models;

public class RiskSignalTests
{
    [Fact]
    public void Create_WithCodeAndDescription_ShouldSetProperties()
    {
        var signal = new RiskSignal { Code = "RISK-1", Description = "Test description" };

        signal.Code.Should().Be("RISK-1");
        signal.Description.Should().Be("Test description");
    }

    [Fact]
    public void Create_WithCodeOnly_ShouldHaveNullDescription()
    {
        var signal = new RiskSignal { Code = "RISK-2" };

        signal.Code.Should().Be("RISK-2");
        signal.Description.Should().BeNull();
    }

    [Fact]
    public void TwoSignalsWithSameValues_ShouldBeEqual()
    {
        var a = new RiskSignal { Code = "C", Description = "D" };
        var b = new RiskSignal { Code = "C", Description = "D" };

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void WithExpression_ShouldCreateModifiedCopy()
    {
        var original = new RiskSignal { Code = "X", Description = "old" };
        var modified = original with { Description = "new" };

        modified.Should().NotBeSameAs(original);
        modified.Code.Should().Be(original.Code);
        modified.Description.Should().Be("new");
        original.Description.Should().Be("old");
    }

    [Fact]
    public void ToString_ShouldContainProperties()
    {
        var signal = new RiskSignal { Code = "ABC", Description = "desc" };

        var text = signal.ToString();

        text.Should().Contain("ABC");
        text.Should().Contain("desc");
    }
}
