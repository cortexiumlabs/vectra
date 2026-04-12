using Vectra.Application.Models;

namespace Vectra.Infrastructure.Semantic;

public interface IAnomalyDetector
{
    Task<double> DetectAsync(RequestContext context, CancellationToken cancellationToken);
}