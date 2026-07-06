using System.Text.Json.Serialization;

namespace AreWeDoomd.ChatProviders.Providers.Gemini.Wire;

internal sealed record GeminiUsageMetadata(
    [property: JsonPropertyName("promptTokenCount")] int PromptTokenCount,
    [property: JsonPropertyName("candidatesTokenCount")] int CandidatesTokenCount,
    [property: JsonPropertyName("thoughtsTokenCount")] int? ThoughtsTokenCount = null);
