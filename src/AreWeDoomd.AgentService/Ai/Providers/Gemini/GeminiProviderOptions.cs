namespace AreWeDoomd.AgentService.Ai.Providers.Gemini;

public sealed class GeminiProviderOptions : ChatProviderOptions
{
    public const string SectionName = "ChatProviders:Gemini";

    /// <summary>
    /// API version segment used in the request path, e.g. <c>v1beta</c>.
    /// </summary>
    public string ApiVersion { get; set; } = "v1beta";
}
