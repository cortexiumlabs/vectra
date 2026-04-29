using System.Text;
using Vectra.Application.Abstractions.Executions;

namespace Vectra.Infrastructure.Semantic.Providers;

public abstract class SemanticProviderBase
{
    protected const string SystemPrompt =
        """
        You are a security intent classifier. Given an HTTP request body, classify the intent into one of:
        bulk_export, destructive_delete, admin_action, harmful, read, write, unknown.
        Respond with a JSON object only, no markdown, in this exact format:
        {"intent":"<label>","confidence":<0.0-1.0>,"risk_tags":["tag1"],"explanation":"<short>"}
        Risk tags: use data_exfiltration, destructive, privilege_escalation, malicious, or empty array.
        """;

    protected static SemanticAnalysisResult ParseResponse(string content, string providerName)
    {
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(content);
            var root = doc.RootElement;
            var intent = root.GetProperty("intent").GetString() ?? "unknown";
            var confidence = root.GetProperty("confidence").GetDouble();
            var explanation = root.TryGetProperty("explanation", out var exp) ? exp.GetString() : null;
            var riskTags = root.TryGetProperty("risk_tags", out var tags)
                ? tags.EnumerateArray().Select(t => t.GetString()!).ToArray()
                : Array.Empty<string>();

            return new SemanticAnalysisResult
            {
                Intent = intent,
                Confidence = confidence,
                RiskTags = riskTags,
                FallbackSafe = confidence < 0.7,
                Explanation = explanation ?? $"{providerName}: {intent} ({confidence:F2})"
            };
        }
        catch
        {
            return new SemanticAnalysisResult { Intent = "unknown", Confidence = 0.5, FallbackSafe = true };
        }
    }

    protected static string ComputeHash(string input) =>
        Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(input)));
}
