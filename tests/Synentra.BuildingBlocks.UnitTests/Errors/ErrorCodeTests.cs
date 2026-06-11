using FluentAssertions;
using Synentra.BuildingBlocks.Errors;
using Xunit;

namespace Synentra.BuildingBlocks.UnitTests.Errors;

public class ErrorCodeTests
{
    [Fact]
    public void ToString_ShouldFormatWithPrefixAndSixDigits()
    {
        var errorCode = new ErrorCode(1, ErrorCategory.System);

        errorCode.ToString().Should().Be("SYN000001");
    }

    [Fact]
    public void ToString_ShouldPadValueToSixDigits()
    {
        var errorCode = new ErrorCode(12345, ErrorCategory.Core);

        errorCode.ToString().Should().Be("SYN012345");
    }

    [Fact]
    public void Prefix_ShouldBeSYN()
    {
        ErrorCode.Prefix.Should().Be("SYN");
    }

    [Fact]
    public void RecordEquality_ShouldConsiderValueAndCategory()
    {
        var code1 = new ErrorCode(1000, ErrorCategory.Core);
        var code2 = new ErrorCode(1000, ErrorCategory.Core);
        var code3 = new ErrorCode(1000, ErrorCategory.Security);

        code1.Should().Be(code2);
        code1.Should().NotBe(code3);
    }

    [Fact]
    public void SynentraErrors_SystemFailure_ShouldHaveCorrectCode()
    {
        SynentraErrors.SystemFailure.Value.Should().Be(1);
        SynentraErrors.SystemFailure.Category.Should().Be(ErrorCategory.System);
    }

    [Fact]
    public void SynentraErrors_ValidationFailed_ShouldHaveCoreCategory()
    {
        SynentraErrors.ValidationFailed.Category.Should().Be(ErrorCategory.Core);
    }
}
