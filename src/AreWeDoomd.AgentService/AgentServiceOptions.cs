namespace AreWeDoomd.AgentService;

public sealed class AgentServiceOptions
{
    public const string SectionName = "AgentNotifications";

    public string HubUrl { get; set; } = string.Empty;

    public string SharedSecret { get; set; } = string.Empty;
}
