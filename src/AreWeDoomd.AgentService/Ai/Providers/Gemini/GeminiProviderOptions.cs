using AreWeDoomd.ChatProviders;

namespace AreWeDoomd.AgentService.Ai.Providers.Gemini;

public sealed class GeminiProviderOptions : ChatProviderOptions
{
    public const string SectionName = "ChatProviders:Gemini";

    /// <summary>API version segment used in the request path, e.g. <c>v1beta</c>.</summary>
    public string ApiVersion { get; set; } = "v1beta";

    /// <summary>How many times to retry after a transient error (429/502/503/504). 0 = no retry.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Base delay in milliseconds for the first retry. Doubles with each attempt.</summary>
    public int RetryBaseDelayMs { get; set; } = 2000;
}
