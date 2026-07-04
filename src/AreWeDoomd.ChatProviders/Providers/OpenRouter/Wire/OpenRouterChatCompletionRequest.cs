using System.Text.Json.Serialization;

namespace AreWeDoomd.AgentService.Ai.Providers.OpenRouter.Wire;

/// <summary>
/// Request body for <c>POST {BaseUrl}/chat/completions</c>. Null optional
/// fields are omitted during serialization.
/// </summary>
internal sealed record OpenRouterChatCompletionRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("messages")]
    public required IReadOnlyList<OpenRouterMessage> Messages { get; init; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; init; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; init; }

    [JsonPropertyName("response_format")]
    public OpenRouterResponseFormat? ResponseFormat { get; init; }
}
