using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synentra.Application.Abstractions.RateLimit;
using Synentra.BuildingBlocks.Configuration.System;
using Synentra.BuildingBlocks.Configuration.System.RateLimit;
using System.Collections.Concurrent;

namespace Synentra.Infrastructure.RateLimit;

/// <summary>
/// Fixed-window per-agent rate limiter backed by an in-process concurrent dictionary.
/// </summary>
public sealed class AgentRateLimiter : IAgentRateLimiter
{
    private static readonly long WindowDurationTicks = TimeSpan.FromMinutes(1).Ticks;

    private sealed class Window
    {
        public int Count;
        public long WindowStartTicks;
    }

    private readonly ConcurrentDictionary<Guid, Window> _windows = new();
    private readonly RateLimitConfiguration _config;
    private readonly ILogger<AgentRateLimiter> _logger;

    public AgentRateLimiter(
        IOptions<SystemConfiguration> options,
        ILogger<AgentRateLimiter> logger)
    {
        _config = options?.Value.RateLimit ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_config.Enabled)
        {
            _logger.LogInformation(
                "Agent rate limiter enabled. Strategy={Strategy}, " +
                "DefaultRequestsPerMinute={DefaultRequestsPerMinute}, " +
                "Storage={Storage}",
                "FixedWindow",
                _config.DefaultRequestsPerMinute,
                "InMemory");
        }
        else
        {
            _logger.LogInformation(
                "Agent rate limiter disabled by configuration.");
        }
    }

    public Task<bool> IsAllowedAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_config.Enabled)
            return Task.FromResult(true);

        var nowTicks = DateTime.UtcNow.Ticks;
        
        var window = _windows.GetOrAdd(agentId, _ => new Window
        {
            Count = 0,
            WindowStartTicks = nowTicks
        });

        lock (window)
        {
            if (nowTicks - window.WindowStartTicks >= WindowDurationTicks)
            {
                window.Count = 1;
                window.WindowStartTicks = nowTicks;

                _logger.LogDebug(
                    "Rate-limit window reset. AgentId={AgentId}, " +
                    "RequestCount={RequestCount}, Limit={Limit}",
                    agentId,
                    window.Count,
                    _config.DefaultRequestsPerMinute);

                return Task.FromResult(true);
            }

            if (window.Count >= _config.DefaultRequestsPerMinute)
            {
                var retryAfter = CalculateRetryAfter(
                    nowTicks,
                    window.WindowStartTicks);

                _logger.LogWarning(
                    "Agent request rejected by rate limiter. " +
                    "AgentId={AgentId}, RequestCount={RequestCount}, " +
                    "Limit={Limit}, RetryAfterSeconds={RetryAfterSeconds}",
                    agentId,
                    window.Count,
                    _config.DefaultRequestsPerMinute,
                    retryAfter.TotalSeconds);

                return Task.FromResult(false);
            }

            window.Count++;

            _logger.LogDebug(
                "Agent request counted by rate limiter. " +
                "AgentId={AgentId}, RequestCount={RequestCount}, Limit={Limit}",
                agentId,
                window.Count,
                _config.DefaultRequestsPerMinute);

            return Task.FromResult(true);
        }
    }

    private static TimeSpan CalculateRetryAfter(
        long nowTicks,
        long windowStartTicks)
    {
        var elapsedTicks = nowTicks - windowStartTicks;
        var remainingTicks = Math.Max(
            0,
            WindowDurationTicks - elapsedTicks);

        return TimeSpan.FromTicks(remainingTicks);
    }
}
