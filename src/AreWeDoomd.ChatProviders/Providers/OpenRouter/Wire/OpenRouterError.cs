using System.Text.Json.Serialization;

namespace AreWeDoomd.ChatProviders.Providers.OpenRouter.Wire;

internal sealed record OpenRouterError(
    [property: JsonPropertyName("message")] string? Message);
