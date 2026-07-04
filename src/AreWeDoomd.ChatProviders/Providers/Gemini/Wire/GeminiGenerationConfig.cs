using System.Text.Json;
using System.Text.Json.Serialization;

namespace AreWeDoomd.ChatProviders.Providers.Gemini.Wire;

internal sealed record GeminiGenerationConfig
{
    [JsonPropertyName("maxOutputTokens")]
    public int? MaxOutputTokens { get; init; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; init; }

    [JsonPropertyName("responseMimeType")]
    public string? ResponseMimeType { get; init; }

    [JsonPropertyName("responseSchema")]
    public JsonElement? ResponseSchema { get; init; }
}
