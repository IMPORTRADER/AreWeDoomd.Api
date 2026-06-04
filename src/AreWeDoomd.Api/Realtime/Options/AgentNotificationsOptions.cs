namespace AreWeDoomd.Api.Realtime.Options;

public sealed class AgentNotificationsOptions
{
    public const string SectionName = "AgentNotifications";

    public string SharedSecret { get; set; } = string.Empty;
}
