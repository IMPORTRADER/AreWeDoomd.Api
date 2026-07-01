namespace AreWeDoomd.AgentService.Ai.Providers.OpenRouter;

public sealed class OpenRouterProviderOptions : ChatProviderOptions
{
    public const string SectionName = "ChatProviders:OpenRouter";

    /// <summary>How many times to retry after a transient error (429/502/503/504). 0 = no retry.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Base delay in milliseconds for the first retry. Doubles with each attempt.</summary>
    public int RetryBaseDelayMs { get; set; } = 2000;

    /// <summary>Optional attribution header (HTTP-Referer) OpenRouter uses for its public rankings. Omitted when blank.</summary>
    public string SiteUrl { get; set; } = string.Empty;

    /// <summary>Optional attribution header (X-Title) OpenRouter uses for its public rankings. Omitted when blank.</summary>
    public string SiteName { get; set; } = string.Empty;
}
