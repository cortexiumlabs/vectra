using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Vectra.Application.Abstractions.RateLimit;
using Vectra.BuildingBlocks.Configuration.System;
using Vectra.BuildingBlocks.Configuration.System.RateLimit;

namespace Vectra.Infrastructure.RateLimit;

/// <summary>
/// Fixed-window per-agent rate limiter backed by an in-process concurrent dictionary.
/// </summary>
public sealed class AgentRateLimiter : IAgentRateLimiter
{
    private sealed class Window
    {
        public int Count;
        public long WindowStartTicks;
    }

    private readonly ConcurrentDictionary<Guid, Window> _windows = new();
    private readonly RateLimitConfiguration _config;

    public AgentRateLimiter(IOptions<SystemConfiguration> options)
    {
        _config = options?.Value.RateLimit 
            ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<bool> IsAllowedAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
            return Task.FromResult(true);

        var windowTicks = TimeSpan.FromMinutes(1).Ticks;
        var nowTicks = DateTime.UtcNow.Ticks;

        var window = _windows.GetOrAdd(agentId, _ => new Window
        {
            Count = 0,
            WindowStartTicks = nowTicks
        });

        lock (window)
        {
            if (nowTicks - window.WindowStartTicks >= windowTicks)
            {
                window.Count = 1;
                window.WindowStartTicks = nowTicks;
                return Task.FromResult(true);
            }

            if (window.Count >= _config.DefaultRequestsPerMinute)
                return Task.FromResult(false);

            window.Count++;
            return Task.FromResult(true);
        }
    }
}
