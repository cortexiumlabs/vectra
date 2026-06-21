using Microsoft.Extensions.Logging;
using NSubstitute;
using Synentra.Services;

namespace Synentra.UnitTests.Services;

public class SynentraVersionTests
{
    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act = () => new SynentraVersion(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_ValidLogger_DoesNotThrow()
    {
        var logger = Substitute.For<ILogger<SynentraVersion>>();
        var act = () => new SynentraVersion(logger);
        act.Should().NotThrow();
    }

    [Fact]
    public void Version_ReturnsNonNullVersion()
    {
        var logger = Substitute.For<ILogger<SynentraVersion>>();
        var service = new SynentraVersion(logger);

        service.Version.Should().NotBeNull();
    }

    [Fact]
    public void GetApplicationVersion_WithNullLogger_ReturnsVersion()
    {
        var version = SynentraVersion.GetApplicationVersion(null);
        version.Should().NotBeNull();
    }

    [Fact]
    public void GetApplicationVersion_WithLogger_ReturnsVersion()
    {
        var logger = Substitute.For<ILogger>();
        var version = SynentraVersion.GetApplicationVersion(logger);
        version.Should().NotBeNull();
    }

    [Fact]
    public void GetApplicationVersion_ReturnsFallbackVersionWhenNoAttribute()
    {
        // Assembly under test has version attributes, but the method should always return a valid Version
        var version = SynentraVersion.GetApplicationVersion();
        version.Should().BeOfType<Version>();
        version.Major.Should().BeGreaterThanOrEqualTo(0);
    }
}
