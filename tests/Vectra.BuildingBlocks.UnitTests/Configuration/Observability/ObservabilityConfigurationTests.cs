using FluentAssertions;
using Vectra.BuildingBlocks.Configuration.Observability;
using Vectra.BuildingBlocks.Configuration.Observability.Logging;
using Vectra.BuildingBlocks.Configuration.Observability.OpenTelemetry;
using Xunit;

namespace Vectra.BuildingBlocks.UnitTests.Configuration.Observability;

public class ObservabilityConfigurationTests
{
    [Fact]
    public void DefaultValues_ShouldInitializeLogging()
    {
        var config = new ObservabilityConfiguration();

        config.Logging.Should().NotBeNull();
    }

    [Fact]
    public void OpenTelemetryConfiguration_DefaultValues_ShouldBeCorrect()
    {
        // Arrange
        var config = new OpenTelemetryConfiguration();

        // Assert
        config.Enabled.Should().BeFalse();
        config.Endpoint.Should().BeNull();
    }

    [Fact]
    public void OpenTelemetryConfiguration_SettingProperties_ShouldBeCorrect()
    {
        // Arrange
        var config = new OpenTelemetryConfiguration
        {
            Enabled = true,
            Endpoint = "http://localhost:4317"
        };

        // Assert
        config.Enabled.Should().BeTrue();
        config.Endpoint.Should().Be("http://localhost:4317");
    }

    [Fact]
    public void LoggingConfiguration_DefaultValues_ShouldBeCorrect()
    {
        var config = new LoggingConfiguration();

        config.DefaultLogLevel.Should().Be("Information");
        config.File.Should().NotBeNull();
        config.Seq.Should().NotBeNull();
    }

    [Fact]
    public void LoggingConfiguration_Create_ShouldReturnConfiguredInstance()
    {
        var config = LoggingConfiguration.Create();

        config.DefaultLogLevel.Should().Be("Information");
        config.File?.LogLevel.Should().Be("Information");
        config.File?.LogPath.Should().Be("logs/log-.txt");
        config.File?.RollingInterval.Should().Be("Day");
        config.File?.RetainedFileCountLimit.Should().Be(7);
        config.Seq?.LogLevel.Should().Be("Information");
    }

    [Fact]
    public void FileLoggingConfiguration_DefaultValues_ShouldBeCorrect()
    {
        var config = new FileLoggingConfiguration();

        config.Enabled.Should().BeTrue();
    }

    [Fact]
    public void FileLoggingConfiguration_Create_ShouldReturnConfiguredInstance()
    {
        var config = FileLoggingConfiguration.Create();

        config.LogLevel.Should().Be("Information");
        config.LogPath.Should().Be("logs/log-.txt");
        config.RollingInterval.Should().Be("Day");
        config.RetainedFileCountLimit.Should().Be(7);
    }

    [Fact]
    public void SeqLoggingConfiguration_DefaultValues_ShouldBeCorrect()
    {
        var config = new SeqLoggingConfiguration();

        config.Enabled.Should().BeFalse();
    }

    [Fact]
    public void SeqLoggingConfiguration_Create_ShouldReturnConfiguredInstance()
    {
        var config = SeqLoggingConfiguration.Create();

        config.LogLevel.Should().Be("Information");
        config.ApiKey.Should().BeNull();
        config.Url.Should().BeNull();
    }

    [Fact]
    public void OtlpLoggingConfiguration_Create_ShouldReturnConfiguredInstance()
    {
        var config = OtlpLoggingConfiguration.Create();

        config.LogLevel.Should().Be("Information");
        config.Endpoint.Should().BeNull();
    }

    [Fact]
    public void OtlpLoggingConfiguration_Should_Allow_Setting_Properties()
    {
        // Arrange
        var config = new OtlpLoggingConfiguration
        {
            Enabled = true,
            Endpoint = "http://localhost:4317",
            LogLevel = "Debug"
        };

        // Assert
        config.Enabled.Should().BeTrue();
        config.Endpoint.Should().Be("http://localhost:4317");
        config.LogLevel.Should().Be("Debug");
    }
}
