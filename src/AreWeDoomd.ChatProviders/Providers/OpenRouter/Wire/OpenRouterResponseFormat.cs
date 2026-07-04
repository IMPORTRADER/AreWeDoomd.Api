using System.Text.Json.Serialization;

namespace AreWeDoomd.ChatProviders.Providers.OpenRouter.Wire;

internal sealed record OpenRouterResponseFormat
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("json_schema")]
    public OpenRouterJsonSchema? JsonSchema { get; init; }
}
