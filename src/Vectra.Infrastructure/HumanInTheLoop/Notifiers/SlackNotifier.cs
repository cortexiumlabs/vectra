using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Vectra.Application.Abstractions.Executions;
using Vectra.BuildingBlocks.Configuration.HumanInTheLoop;

namespace Vectra.Infrastructure.HumanInTheLoop.Notifiers;

/// <summary>
/// Sends HITL notifications to Slack via incoming webhooks.
/// </summary>
public class SlackNotifier : IHitlNotifier
{
    private readonly SlackNotificationConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SlackNotifier> _logger;

    public SlackNotifier(
        IOptions<HumanInTheLoopConfiguration> config,
        IHttpClientFactory httpClientFactory,
        ILogger<SlackNotifier> logger)
    {
        _config = config?.Value?.Notifications?.Slack ?? throw new ArgumentNullException(nameof(config));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task NotifyAsync(HitlNotification notification, CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled || string.IsNullOrWhiteSpace(_config.WebhookUrl))
        {
            _logger.LogDebug("Slack notifications are disabled or webhook URL is not configured");
            return;
        }

        try
        {
            var payload = BuildSlackPayload(notification);
            var httpClient = _httpClientFactory.CreateClient();

            var response = await httpClient.PostAsJsonAsync(_config.WebhookUrl, payload, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Slack notification for HITL request {HitlId} failed with status {StatusCode}: {Error}",
                    notification.Id, (int)response.StatusCode, errorContent);
            }
            else
            {
                _logger.LogInformation("Slack notification sent successfully for HITL request {HitlId}", notification.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Slack notification for HITL request {HitlId}", notification.Id);
        }
    }

    private SlackPayload BuildSlackPayload(HitlNotification notification)
    {
        var expiresInMinutes = (int)(notification.ExpiresAt - notification.Timestamp).TotalMinutes;

        var text = $"🚨 *HITL Review Required*\n\n" +
                   $"*Request ID:* `{notification.Id}`\n" +
                   $"*Agent ID:* `{notification.AgentId}`\n" +
                   $"*Method:* `{notification.Method}`\n" +
                   $"*URL:* `{notification.Url}`\n" +
                   $"*Reason:* {notification.Reason}\n" +
                   $"*Expires In:* {expiresInMinutes} minutes\n" +
                   $"*Timestamp:* {notification.Timestamp:yyyy-MM-dd HH:mm:ss} UTC";

        return new SlackPayload
        {
            Text = text,
            Username = _config.Username,
            IconEmoji = _config.IconEmoji,
            Channel = _config.Channel
        };
    }

    private record SlackPayload
    {
        [JsonPropertyName("text")]
        public string? Text { get; init; }

        [JsonPropertyName("username")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Username { get; init; }

        [JsonPropertyName("icon_emoji")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? IconEmoji { get; init; }

        [JsonPropertyName("channel")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Channel { get; init; }
    }
}
