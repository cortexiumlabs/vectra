using Vectra.BuildingBlocks.Configuration.System.CircuitBreaker;
using Vectra.BuildingBlocks.Configuration.System.RateLimit;
using Vectra.BuildingBlocks.Configuration.System.Server;

namespace Vectra.BuildingBlocks.Configuration.System;

public class SystemConfiguration
{
    public ServerConfiguration Server { get; set; } = new();
    public StorageConfiguration Storage { get; set; } = new();
    public RateLimitConfiguration RateLimit { get; set; } = new();
    public CircuitBreakerConfiguration CircuitBreaker { get; set; } = new();
}