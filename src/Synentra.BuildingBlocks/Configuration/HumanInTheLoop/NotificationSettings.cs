namespace Synentra.BuildingBlocks.Configuration.HumanInTheLoop;

public class NotificationSettings
{
    /// <summary>Slack notification configuration.</summary>
    public SlackNotificationConfiguration Slack { get; set; } = new();

    /// <summary>Microsoft Teams notification configuration.</summary>
    public TeamsNotificationConfiguration Teams { get; set; } = new();

    /// <summary>PagerDuty notification configuration.</summary>
    public PagerDutyNotificationConfiguration PagerDuty { get; set; } = new();

    /// <summary>Generic webhook notification configuration.</summary>
    public GenericWebhookNotificationConfiguration GenericWebhook { get; set; } = new();
}