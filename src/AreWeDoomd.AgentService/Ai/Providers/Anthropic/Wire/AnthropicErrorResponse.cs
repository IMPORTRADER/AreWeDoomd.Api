using System.Text.Json.Serialization;

namespace AreWeDoomd.AgentService.Ai.Providers.Anthropic.Wire;

/// <summary>
/// Error envelope returned by the Anthropic API on non-2xx responses, e.g.
/// <c>{"type":"error","error":{"type":"...","message":"..."}}</c>.
/// </summary>
internal sealed record AnthropicErrorResponse(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("error")] AnthropicError? Error);
