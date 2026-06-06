namespace AreWeDoomd.AgentService.Ai;

/// <summary>
/// Common configuration shared by every chat provider adapter. Concrete
/// providers derive from this and add provider-specific settings.
/// </summary>
public abstract class ChatProviderOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public string DefaultModel { get; set; } = string.Empty;

    public int DefaultMaxTokens { get; set; }
}
