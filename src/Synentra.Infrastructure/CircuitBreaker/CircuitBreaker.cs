using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synentra.Application.Abstractions.CircuitBreaker;
using Synentra.BuildingBlocks.Configuration.System;
using Synentra.BuildingBlocks.Configuration.System.CircuitBreaker;
using System.Collections.Concurrent;

namespace Synentra.Infrastructure.CircuitBreaker;

/// <summary>
/// Simple per-host circuit breaker (Closed → Open → HalfOpen → Closed).
/// Thread-safe, singleton-scoped.
/// </summary>
public sealed class CircuitBreaker : ICircuitBreaker
{
    private enum State { Closed, Open, HalfOpen }

    private sealed class HostCircuit
    {
        public State State = State.Closed;
        public int FailureCount;
        public DateTime OpenedAt;
        public DateTime WindowStart = DateTime.UtcNow;
    }

    private readonly ConcurrentDictionary<string, HostCircuit> _circuits = new(StringComparer.OrdinalIgnoreCase);
    private readonly CircuitBreakerConfiguration _config;
    private readonly ILogger<CircuitBreaker> _logger;

    public CircuitBreaker(
        IOptions<SystemConfiguration> options,
        ILogger<CircuitBreaker> logger)
    {
        _config = options?.Value.CircuitBreaker ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_config.Enabled)
        {
            _logger.LogInformation(
                "Circuit breaker enabled. FailureThreshold={FailureThreshold}, " +
                "SamplingWindowSeconds={SamplingWindowSeconds}, " +
                "OpenDurationSeconds={OpenDurationSeconds}",
                _config.FailureThreshold,
                _config.SamplingWindowSeconds,
                _config.OpenDurationSeconds);
        }
        else
        {
            _logger.LogInformation(
                "Circuit breaker disabled by configuration.");
        }
    }

    public bool IsAllowed(string host)
    {
        if (!_config.Enabled)
            return true;

        var circuit = _circuits.GetOrAdd(host, _ => new HostCircuit());

        lock (circuit)
        {
            if (circuit.State == State.Closed)
                return true;

            if (circuit.State == State.Open)
            {
                var elapsed = (DateTime.UtcNow - circuit.OpenedAt).TotalSeconds;
                if (elapsed >= _config.OpenDurationSeconds)
                {
                    circuit.State = State.HalfOpen;
                    _logger.LogInformation("Circuit breaker transitioned to HalfOpen. Host={Host}", host);
                    return true; // probe request
                }
                return false;
            }

            // HalfOpen – allow one probe
            return true;
        }
    }

    public void RecordSuccess(string host)
    {
        if (!_config.Enabled) return;

        var circuit = _circuits.GetOrAdd(host, _ => new HostCircuit());
        lock (circuit)
        {
            var previousState = circuit.State;

            circuit.State = State.Closed;
            circuit.FailureCount = 0;
            circuit.WindowStart = DateTime.UtcNow;

            if (previousState is State.Open or State.HalfOpen)
            {
                _logger.LogInformation(
                    "Circuit breaker transitioned to Closed after a successful request. " +
                    "Host={Host}, PreviousState={PreviousState}",
                    host,
                    previousState);
            }
        }
    }

    public void RecordFailure(string host)
    {
        if (!_config.Enabled) return;

        var circuit = _circuits.GetOrAdd(host, _ => new HostCircuit());
        lock (circuit)
        {
            var now = DateTime.UtcNow;

            // Reset window if expired
            if ((now - circuit.WindowStart).TotalSeconds >= _config.SamplingWindowSeconds)
            {
                circuit.FailureCount = 0;
                circuit.WindowStart = now;
            }

            circuit.FailureCount++;

            _logger.LogDebug(
                "Circuit breaker recorded a failure. Host={Host}, " +
                "FailureCount={FailureCount}, FailureThreshold={FailureThreshold}",
                host,
                circuit.FailureCount,
                _config.FailureThreshold);

            if (circuit.FailureCount < _config.FailureThreshold)
                return;

            if (circuit.State != State.Open)
            {
                var previousState = circuit.State;

                circuit.State = State.Open;
                circuit.OpenedAt = now;

                _logger.LogWarning(
                    "Circuit breaker transitioned to Open. " +
                    "Host={Host}, PreviousState={PreviousState}, " +
                    "FailureCount={FailureCount}, OpenDurationSeconds={OpenDurationSeconds}",
                    host,
                    previousState,
                    circuit.FailureCount,
                    _config.OpenDurationSeconds);
            }
        }
    }
}
