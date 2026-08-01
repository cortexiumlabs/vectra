using System.Text.Json.Serialization;

namespace Synentra.Infrastructure.Policy.Opa;

public sealed record OpaDecisionInput
{
    [JsonPropertyName("agent")]
    public required AgentInput Agent { get; init; }

    [JsonPropertyName("request")]
    public required RequestInput Request { get; init; }

    [JsonPropertyName("intent")]
    public required IntentInput Intent { get; init; }

    [JsonPropertyName("risk")]
    public required RiskInput Risk { get; init; }

    public sealed record AgentInput
    {
        [JsonPropertyName("id")]
        public required Guid Id { get; init; }

        [JsonPropertyName("roles")]
        public required IReadOnlyCollection<string> Roles { get; init; }

        [JsonPropertyName("scopes")]
        public required IReadOnlyCollection<string> Scopes { get; init; }

        [JsonPropertyName("trustScore")]
        public required double TrustScore { get; init; }
    }

    public sealed record RequestInput
    {
        [JsonPropertyName("method")]
        public required string Method { get; init; }

        [JsonPropertyName("path")]
        public required string Path { get; init; }

        [JsonPropertyName("contentType")]
        public string? ContentType { get; init; }

        [JsonPropertyName("contentLength")]
        public int ContentLength { get; init; }
    }

    public sealed record IntentInput
    {
        [JsonPropertyName("label")]
        public required string Label { get; init; }

        [JsonPropertyName("originalLabel")]
        public string? OriginalLabel { get; init; }

        [JsonPropertyName("confidence")]
        public required double Confidence { get; init; }

        [JsonPropertyName("status")]
        public required string Status { get; init; }

        [JsonPropertyName("modelVersion")]
        public string? ModelVersion { get; init; }
    }

    public sealed record RiskInput
    {
        [JsonPropertyName("score")]
        public required double Score { get; init; }

        [JsonPropertyName("level")]
        public required string Level { get; init; }

        [JsonPropertyName("signals")]
        public required IReadOnlyCollection<string> Signals { get; init; }
    }
}
