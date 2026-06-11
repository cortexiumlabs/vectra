using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Vectra.BuildingBlocks.Configuration.System;
using Vectra.BuildingBlocks.Configuration.System.CircuitBreaker;
using Vectra.Infrastructure.CircuitBreaker;

namespace Vectra.Infrastructure.UnitTests.CircuitBreaker;

public class CircuitBreakerTests
{
    private readonly IOptions<SystemConfiguration> _options = Substitute.For<IOptions<SystemConfiguration>>();
    private readonly Vectra.Infrastructure.CircuitBreaker.CircuitBreaker _circuitBreaker;

    public CircuitBreakerTests()
    {
        _options.Value.Returns(new SystemConfiguration
        {
            CircuitBreaker = new CircuitBreakerConfiguration
            {
                Enabled = true,
                FailureThreshold = 3,
                OpenDurationSeconds = 10,
                SamplingWindowSeconds = 30
            }
        });
        _circuitBreaker = new Vectra.Infrastructure.CircuitBreaker.CircuitBreaker(_options);
    }

    [Fact]
    public void IsAllowed_WhenDisabled_ShouldReturnTrue()
    {
        // Arrange
        _options.Value.Returns(new SystemConfiguration { CircuitBreaker = new CircuitBreakerConfiguration { Enabled = false } });
        var cb = new Vectra.Infrastructure.CircuitBreaker.CircuitBreaker(_options);

        // Act
        var isAllowed = cb.IsAllowed("test-host");

        // Assert
        isAllowed.Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_WhenClosed_ShouldReturnTrue()
    {
        // Act
        var isAllowed = _circuitBreaker.IsAllowed("test-host");

        // Assert
        isAllowed.Should().BeTrue();
    }

    [Fact]
    public void RecordFailure_BelowThreshold_ShouldRemainClosed()
    {
        // Act
        _circuitBreaker.RecordFailure("test-host");
        _circuitBreaker.RecordFailure("test-host");

        // Assert
        _circuitBreaker.IsAllowed("test-host").Should().BeTrue();
    }

    [Fact]
    public void RecordFailure_AtThreshold_ShouldOpen()
    {
        // Act
        _circuitBreaker.RecordFailure("test-host");
        _circuitBreaker.RecordFailure("test-host");
        _circuitBreaker.RecordFailure("test-host");

        // Assert
        _circuitBreaker.IsAllowed("test-host").Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_WhenOpen_ShouldReturnFalse()
    {
        // Arrange
        _circuitBreaker.RecordFailure("test-host");
        _circuitBreaker.RecordFailure("test-host");
        _circuitBreaker.RecordFailure("test-host");

        // Act
        var isAllowed = _circuitBreaker.IsAllowed("test-host");

        // Assert
        isAllowed.Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_WhenHalfOpen_ShouldReturnTrueForProbe()
    {
        // Arrange
        _circuitBreaker.RecordFailure("test-host");
        _circuitBreaker.RecordFailure("test-host");
        _circuitBreaker.RecordFailure("test-host");

        // Wait for the circuit to enter half-open state
        Thread.Sleep(TimeSpan.FromSeconds(11));

        // Act
        var isAllowed = _circuitBreaker.IsAllowed("test-host");

        // Assert
        isAllowed.Should().BeTrue();
    }

    [Fact]
    public void RecordSuccess_WhenHalfOpen_ShouldCloseCircuit()
    {
        // Arrange
        _circuitBreaker.RecordFailure("test-host");
        _circuitBreaker.RecordFailure("test-host");
        _circuitBreaker.RecordFailure("test-host");
        Thread.Sleep(TimeSpan.FromSeconds(11)); // Enter half-open
        _circuitBreaker.IsAllowed("test-host"); // Probe

        // Act
        _circuitBreaker.RecordSuccess("test-host");

        // Assert
        _circuitBreaker.IsAllowed("test-host").Should().BeTrue();
    }

    [Fact]
    public void RecordFailure_ResetsAfterWindow()
    {
        // Arrange
        _circuitBreaker.RecordFailure("test-host");
        _circuitBreaker.RecordFailure("test-host");

        // Wait for the sampling window to reset
        Thread.Sleep(TimeSpan.FromSeconds(31));

        _circuitBreaker.RecordFailure("test-host");

        // Assert
        _circuitBreaker.IsAllowed("test-host").Should().BeTrue();
    }
}
