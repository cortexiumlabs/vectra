using Synentra.BuildingBlocks.Configuration.Semantic;

namespace Synentra.Infrastructure.Semantic.Providers.InternalBert;

public interface IModelDownloader
{
    Task EnsureModelExistsAsync(InternalOnnxConfiguration config, CancellationToken cancellationToken);
}