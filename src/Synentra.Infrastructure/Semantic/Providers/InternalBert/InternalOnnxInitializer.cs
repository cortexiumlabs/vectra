using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synentra.BuildingBlocks.Configuration.Semantic;

namespace Synentra.Infrastructure.Semantic.Providers.InternalBert;

internal sealed class InternalOnnxInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<SemanticConfiguration> _options;
    private readonly ILogger<InternalOnnxInitializer> _logger;

    public InternalOnnxInitializer(
        IServiceProvider serviceProvider,
        IOptions<SemanticConfiguration> options,
        ILogger<InternalOnnxInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var config = _options.Value;
        if (config.Enabled != true)
            return Task.CompletedTask;

        var provider = (config.DefaultProvider ?? "internal").Trim();
        if (!provider.Equals("internal", StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        _ = _serviceProvider.GetRequiredService<InternalOnnxProvider>();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
