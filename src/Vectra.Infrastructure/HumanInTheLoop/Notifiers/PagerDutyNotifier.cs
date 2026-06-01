using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Vectra.Application.Abstractions.Executions;
using Vectra.BuildingBlocks.Configuration.HumanInTheLoop;

namespace Vectra.Infrastructure.HumanInTheLoop.Notifiers;

/// <summary>
/// Sends HITL notifications to PagerDuty via Events API v2.
/// </summary>
public class PagerDutyNotifier : IHitlNotifier
{
    private readonly PagerDutyNotificationConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PagerDutyNotifier> _logger;

    public PagerDutyNotifier(
        IOptions<HumanInTheLoopConfiguration> config,
        IHttpClientFactory httpClientFactory,
        ILogger<PagerDutyNotifier> logger)
    {
        _config = config?.Value?.Notifications?.PagerDuty ?? throw new ArgumentNullException(nameof(config));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task NotifyAsync(HitlNotification notification, CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled || string.IsNullOrWhiteSpace(_config.RoutingKey))
        {
            _logger.LogDebug("PagerDuty notifications are disabled or routing key is not configured");
            return;
        }

        try
        {
            var payload = BuildPagerDutyPayload(notification);
            var httpClient = _httpClientFactory.CreateClient();

            var response = await httpClient.PostAsJsonAsync(_config.ApiUrl, payload, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "PagerDuty notification for HITL request {HitlId} failed with status {StatusCode}: {Error}",
                    notification.Id, (int)response.StatusCode, errorContent);
            }
            else
            {
                _logger.LogInformation("PagerDuty notification sent successfully for HITL request {HitlId}", notification.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send PagerDuty notification for HITL request {HitlId}", notification.Id);
        }
    }

    private PagerDutyEvent BuildPagerDutyPayload(HitlNotification notification)
    {
        var expiresInMinutes = (int)(notification.ExpiresAt - notification.Timestamp).TotalMinutes;

        return new PagerDutyEvent
        {
            RoutingKey = _config.RoutingKey!,
            EventAction = "trigger",
            DedupKey = $"hitl-{notification.Id}",
            Payload = new PagerDutyPayload
            {
                Summary = $"HITL Review Required: {notification.Method} {notification.Url}",
                Source = "Vectra",
                Severity = _config.Severity,
                Timestamp = notification.Timestamp,
                Component = "HITL",
                Group = notification.AgentId.ToString(),
                Class = "HITL Request",
                CustomDetails = new Dictionary<string, object>
                {
                    { "request_id", notification.Id },
                    { "agent_id", notification.AgentId.ToString() },
                    { "method", notification.Method },
                    { "url", notification.Url },
                    { "reason", notification.Reason },
                    { "expires_in_minutes", expiresInMinutes },
                    { "expires_at", notification.ExpiresAt }
                }
            }
        };
    }

    private record PagerDutyEvent
    {
        [JsonPropertyName("routing_key")]
        public string? RoutingKey { get; init; }

        [JsonPropertyName("event_action")]
        public string? EventAction { get; init; }

        [JsonPropertyName("dedup_key")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DedupKey { get; init; }

        [JsonPropertyName("payload")]
        public PagerDutyPayload? Payload { get; init; }
    }

    private record PagerDutyPayload
    {
        [JsonPropertyName("summary")]
        public string? Summary { get; init; }

        [JsonPropertyName("source")]
        public string? Source { get; init; }

        [JsonPropertyName("severity")]
        public string? Severity { get; init; }

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; init; }

        [JsonPropertyName("component")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Component { get; init; }

        [JsonPropertyName("group")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Group { get; init; }

        [JsonPropertyName("class")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Class { get; init; }

        [JsonPropertyName("custom_details")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, object>? CustomDetails { get; init; }
    }
}
