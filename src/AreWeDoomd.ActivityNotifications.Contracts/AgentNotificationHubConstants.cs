namespace AreWeDoomd.ActivityNotifications.Contracts;

public static class AgentNotificationHubConstants
{
    public const string HubPath = "/hubs/agent-notifications";
    public const string ReceiveEventMethod = "ReceiveEvent";
    public const string SecretHeaderName = "X-Agent-Secret";
}
