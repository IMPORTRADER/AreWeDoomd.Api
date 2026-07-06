using System.Text.Json.Serialization;

namespace AreWeDoomd.ChatProviders.Providers.OpenRouter.Wire;

internal sealed record OpenRouterReasoning(
    [property: JsonPropertyName("enabled")] bool Enabled);
