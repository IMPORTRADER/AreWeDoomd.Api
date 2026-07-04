using System.Text.Json.Serialization;

namespace AreWeDoomd.AgentService.Ai.Providers.OpenRouter.Wire;

internal sealed record OpenRouterError(
    [property: JsonPropertyName("message")] string? Message);
