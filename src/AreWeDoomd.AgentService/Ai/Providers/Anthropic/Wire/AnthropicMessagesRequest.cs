using System.Text.Json.Serialization;

namespace AreWeDoomd.AgentService.Ai.Providers.Anthropic.Wire;

/// <summary>
/// Request body for <c>POST /v1/messages</c>. Null optional fields are omitted
/// during serialization so the wire payload stays minimal.
/// </summary>
internal sealed record AnthropicMessagesRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("max_tokens")]
    public required int MaxTokens { get; init; }

    [JsonPropertyName("messages")]
    public required IReadOnlyList<AnthropicRequestMessage> Messages { get; init; }

    [JsonPropertyName("system")]
    public string? System { get; init; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; init; }
}
