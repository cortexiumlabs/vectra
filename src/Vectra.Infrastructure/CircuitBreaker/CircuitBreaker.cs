using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Vectra.Application.Abstractions.CircuitBreaker;
using Vectra.BuildingBlocks.Configuration.System;
using Vectra.BuildingBlocks.Configuration.System.CircuitBreaker;

namespace Vectra.Infrastructure.CircuitBreaker;

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

    public CircuitBreaker(IOptions<SystemConfiguration> options)
    {
        _config = options?.Value.CircuitBreaker 
            ?? throw new ArgumentNullException(nameof(options));
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
            circuit.State = State.Closed;
            circuit.FailureCount = 0;
            circuit.WindowStart = DateTime.UtcNow;
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

            if (circuit.FailureCount >= _config.FailureThreshold)
            {
                circuit.State = State.Open;
                circuit.OpenedAt = now;
            }
        }
    }
}
