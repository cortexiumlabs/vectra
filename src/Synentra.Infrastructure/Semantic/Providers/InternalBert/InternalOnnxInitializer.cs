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

        // Resolve the singleton provider (this triggers its constructor registration)
        var onnxProvider = _serviceProvider.GetRequiredService<InternalOnnxProvider>();

        // Fire‑and‑forget the actual async initialisation (download + model load)
        _ = Task.Run(async () =>
        {
            try
            {
                await onnxProvider.InitializeAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialise Internal ONNX provider.");
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}