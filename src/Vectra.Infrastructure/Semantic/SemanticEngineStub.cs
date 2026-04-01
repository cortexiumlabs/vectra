using Vectra.Core.Interfaces;

namespace Vectra.Infrastructure.Semantic;

public class SemanticEngineStub : ISemanticEngine
{
    public Task<SemanticResult> AnalyzeAsync(string? body, string metadata, CancellationToken cancellationToken = default)
    {
        string intent = "normal";
        double confidence = 0.88;
        string[] riskTags = Array.Empty<string>();

        if (body != null && body.Contains("export", StringComparison.OrdinalIgnoreCase))
        {
            intent = "bulk_export";
            confidence = 0.92;
            riskTags = new[] { "data_exfiltration" };
        }

        return Task.FromResult(new SemanticResult(intent, confidence, riskTags, false));
    }
}