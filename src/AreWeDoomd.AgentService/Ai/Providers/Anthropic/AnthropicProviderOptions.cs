namespace AreWeDoomd.AgentService.Ai.Providers.Anthropic;

public sealed class AnthropicProviderOptions : ChatProviderOptions
{
    public const string SectionName = "ChatProviders:Anthropic";

    /// <summary>
    /// Value sent in the required <c>anthropic-version</c> header.
    /// </summary>
    public string AnthropicVersion { get; set; } = "2023-06-01";
}
