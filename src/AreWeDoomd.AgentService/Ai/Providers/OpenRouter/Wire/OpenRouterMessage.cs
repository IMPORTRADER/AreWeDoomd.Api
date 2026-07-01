using System.Text.Json.Serialization;

namespace AreWeDoomd.AgentService.Ai.Providers.OpenRouter.Wire;

/// <summary>
/// A single chat-completions message. Used for both request turns (role
/// "system"/"user") and the response message (role "assistant").
/// </summary>
internal sealed record OpenRouterMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);
