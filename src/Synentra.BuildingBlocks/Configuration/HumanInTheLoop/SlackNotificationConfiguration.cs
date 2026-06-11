namespace Synentra.BuildingBlocks.Configuration.HumanInTheLoop;

/// <summary>
/// Configuration for Slack notifications.
/// </summary>
public class SlackNotificationConfiguration
{
    /// <summary>Whether Slack notifications are enabled.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Slack webhook URL (incoming webhook).</summary>
    public string? WebhookUrl { get; set; }

    /// <summary>Optional channel to post to (overrides webhook default).</summary>
    public string? Channel { get; set; }

    /// <summary>Optional username for the bot.</summary>
    public string? Username { get; set; } = "Synentra HITL";

    /// <summary>Optional icon emoji for the bot.</summary>
    public string? IconEmoji { get; set; } = ":robot_face:";
}