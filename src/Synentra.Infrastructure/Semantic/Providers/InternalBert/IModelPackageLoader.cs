using Synentra.BuildingBlocks.Configuration.Semantic;

namespace Synentra.Infrastructure.Semantic.Providers.InternalBert;

public interface IModelPackageLoader
{
    Task<ModelAssets> LoadAsync(InternalOnnxConfiguration config, CancellationToken cancellationToken);
}