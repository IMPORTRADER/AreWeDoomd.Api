using System.Text.Json.Serialization;

namespace AreWeDoomd.AgentService.Ai.Providers.Anthropic.Wire;

internal sealed record AnthropicContentBlock(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("text")] string? Text);
