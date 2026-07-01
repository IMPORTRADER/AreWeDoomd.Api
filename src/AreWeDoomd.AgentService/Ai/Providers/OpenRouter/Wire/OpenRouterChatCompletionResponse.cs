using System.Text.Json.Serialization;

namespace AreWeDoomd.AgentService.Ai.Providers.OpenRouter.Wire;

/// <summary>
/// Successful response body from <c>chat/completions</c>. Only the fields the
/// adapter consumes are modelled; everything else is ignored.
/// </summary>
internal sealed record OpenRouterChatCompletionResponse
{
    [JsonPropertyName("choices")]
    public IReadOnlyList<OpenRouterChoice>? Choices { get; init; }

    [JsonPropertyName("usage")]
    public OpenRouterUsage? Usage { get; init; }
}
