using System.Text.Json.Serialization;

namespace AreWeDoomd.AgentService.Ai.Providers.OpenRouter.Wire;

/// <summary>
/// Error envelope returned by OpenRouter on non-2xx responses, e.g.
/// <c>{"error":{"message":"...","code":429}}</c>.
/// </summary>
internal sealed record OpenRouterErrorResponse(
    [property: JsonPropertyName("error")] OpenRouterError? Error);
