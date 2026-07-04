using System.Text.Json.Serialization;

namespace AreWeDoomd.AgentService.Ai.Providers.OpenRouter.Wire;

internal sealed record OpenRouterResponseMessage(
    [property: JsonPropertyName("role")] string? Role,
    [property: JsonPropertyName("content")] string? Content);
