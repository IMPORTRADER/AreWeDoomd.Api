using System.Text.Json.Serialization;

namespace AreWeDoomd.AgentService.Ai.Providers.Anthropic.Wire;

/// <summary>
/// A single turn in the Anthropic <c>messages</c> array. Internal to the
/// Anthropic adapter — provider wire shapes never leave this assembly boundary.
/// </summary>
internal sealed record AnthropicRequestMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);
