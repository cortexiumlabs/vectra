namespace Synentra.BuildingBlocks.Configuration.HumanInTheLoop;

/// <summary>
/// Configuration for PagerDuty notifications.
/// </summary>
public class PagerDutyNotificationConfiguration
{
    /// <summary>Whether PagerDuty notifications are enabled.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>PagerDuty Events API v2 routing key (integration key).</summary>
    public string? RoutingKey { get; set; }

    /// <summary>PagerDuty Events API URL.</summary>
    public string ApiUrl { get; set; } = "https://events.pagerduty.com/v2/enqueue";

    /// <summary>Severity level for the alert.</summary>
    public string Severity { get; set; } = "warning";
}