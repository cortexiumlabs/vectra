using Synentra.BuildingBlocks.Configuration.System.CircuitBreaker;
using Synentra.BuildingBlocks.Configuration.System.Cors;
using Synentra.BuildingBlocks.Configuration.System.RateLimit;
using Synentra.BuildingBlocks.Configuration.System.Server;

namespace Synentra.BuildingBlocks.Configuration.System;

public class SystemConfiguration
{
    public ServerConfiguration Server { get; set; } = new();
    public StorageConfiguration Storage { get; set; } = new();
    public RateLimitConfiguration RateLimit { get; set; } = new();
    public CircuitBreakerConfiguration CircuitBreaker { get; set; } = new();
    public CorsConfiguration Cors { get; set; } = new();
}