using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;
using Vectra.Application.Abstractions.Executions;
using Vectra.BuildingBlocks.Configuration.HumanInTheLoop;

namespace Vectra.Infrastructure.HumanInTheLoop.Notifiers;

/// <summary>
/// Sends HITL notifications to Microsoft Teams via incoming webhooks (MessageCard format).
/// </summary>
public class TeamsNotifier : NotifierBase<TeamsNotifier.TeamsMessageCard>
{
    private readonly TeamsNotificationConfiguration _config;

    public TeamsNotifier(
        IOptions<HumanInTheLoopConfiguration> config,
        IHttpClientFactory httpClientFactory,
        ILogger<TeamsNotifier> logger)
        : base(httpClientFactory, logger)
    {
        _config = config?.Value?.Notifications?.Teams ?? throw new ArgumentNullException(nameof(config));
    }

    protected override bool IsEnabled() => _config.Enabled && !string.IsNullOrWhiteSpace(_config.WebhookUrl);

    protected override string GetWebhookUrl() => _config.WebhookUrl!;

    protected override string GetNotifierType() => "Teams";

    protected override TeamsMessageCard BuildPayload(HitlNotification notification)
    {
        var expiresInMinutes = (int)(notification.ExpiresAt - notification.Timestamp).TotalMinutes;

        return new TeamsMessageCard
        {
            Type = "MessageCard",
            Context = "https://schema.org/extensions",
            ThemeColor = _config.ThemeColor,
            Summary = "HITL Review Required",
            Sections = new[]
            {
                new TeamsSection
                {
                    ActivityTitle = "🚨 HITL Review Required",
                    ActivitySubtitle = $"Request ID: {notification.Id}",
                    Facts = new[]
                    {
                        new TeamsFact { Name = "Agent ID", Value = notification.AgentId.ToString() },
                        new TeamsFact { Name = "Method", Value = notification.Method },
                        new TeamsFact { Name = "URL", Value = notification.Url },
                        new TeamsFact { Name = "Reason", Value = notification.Reason },
                        new TeamsFact { Name = "Expires In", Value = $"{expiresInMinutes} minutes" },
                        new TeamsFact { Name = "Timestamp", Value = $"{notification.Timestamp:yyyy-MM-dd HH:mm:ss} UTC" }
                    }
                }
            }
        };
    }

    public record TeamsMessageCard
    {
        [JsonPropertyName("@type")]
        public string? Type { get; init; }

        [JsonPropertyName("@context")]
        public string? Context { get; init; }

        [JsonPropertyName("themeColor")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ThemeColor { get; init; }

        [JsonPropertyName("summary")]
        public string? Summary { get; init; }

        [JsonPropertyName("sections")]
        public TeamsSection[]? Sections { get; init; }
    }

    public record TeamsSection
    {
        [JsonPropertyName("activityTitle")]
        public string? ActivityTitle { get; init; }

        [JsonPropertyName("activitySubtitle")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ActivitySubtitle { get; init; }

        [JsonPropertyName("facts")]
        public TeamsFact[]? Facts { get; init; }
    }

    public record TeamsFact
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("value")]
        public string? Value { get; init; }
    }
}
