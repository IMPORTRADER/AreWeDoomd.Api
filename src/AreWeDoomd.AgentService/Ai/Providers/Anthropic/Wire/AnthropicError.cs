using System.Text.Json.Serialization;

namespace AreWeDoomd.AgentService.Ai.Providers.Anthropic.Wire;

internal sealed record AnthropicError(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("message")] string? Message);
