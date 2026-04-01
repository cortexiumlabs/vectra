namespace Vectra.Core.Interfaces;

public interface ISemanticEngine
{
    Task<SemanticResult> AnalyzeAsync(string? body, string metadata, CancellationToken cancellationToken = default);
}

public record SemanticResult(string Intent, double Confidence, string[] RiskTags, bool FallbackSafe);