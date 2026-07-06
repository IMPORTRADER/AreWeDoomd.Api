using System.Text.Json.Serialization;

namespace AreWeDoomd.ChatProviders.Providers.Gemini.Wire;

internal sealed record GeminiError(
    [property: JsonPropertyName("code")] int? Code,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("status")] string? Status);
