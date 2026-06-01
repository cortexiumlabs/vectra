using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Vectra.Application.Abstractions.Executions;
using Vectra.BuildingBlocks.Configuration.HumanInTheLoop;

namespace Vectra.Infrastructure.HumanInTheLoop.Notifiers;

/// <summary>
/// Sends HITL notifications to Microsoft Teams via incoming webhooks (MessageCard format).
/// </summary>
public class TeamsNotifier : IHitlNotifier
{
    private readonly TeamsNotificationConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TeamsNotifier> _logger;

    public TeamsNotifier(
        IOptions<HumanInTheLoopConfiguration> config,
        IHttpClientFactory httpClientFactory,
        ILogger<TeamsNotifier> logger)
    {
        _config = config?.Value?.Notifications?.Teams ?? throw new ArgumentNullException(nameof(config));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task NotifyAsync(HitlNotification notification, CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled || string.IsNullOrWhiteSpace(_config.WebhookUrl))
        {
            _logger.LogDebug("Teams notifications are disabled or webhook URL is not configured");
            return;
        }

        try
        {
            var payload = BuildTeamsPayload(notification);
            var httpClient = _httpClientFactory.CreateClient();

            var response = await httpClient.PostAsJsonAsync(_config.WebhookUrl, payload, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Teams notification for HITL request {HitlId} failed with status {StatusCode}: {Error}",
                    notification.Id, (int)response.StatusCode, errorContent);
            }
            else
            {
                _logger.LogInformation("Teams notification sent successfully for HITL request {HitlId}", notification.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Teams notification for HITL request {HitlId}", notification.Id);
        }
    }

    private TeamsMessageCard BuildTeamsPayload(HitlNotification notification)
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

    private record TeamsMessageCard
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

    private record TeamsSection
    {
        [JsonPropertyName("activityTitle")]
        public string? ActivityTitle { get; init; }

        [JsonPropertyName("activitySubtitle")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ActivitySubtitle { get; init; }

        [JsonPropertyName("facts")]
        public TeamsFact[]? Facts { get; init; }
    }

    private record TeamsFact
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("value")]
        public string? Value { get; init; }
    }
}
