using System.Text.Json.Serialization;

namespace AreWeDoomd.AgentService.Ai.Providers.Gemini.Wire;

/// <summary>
/// Request body for <c>POST /v1beta/models/{model}:generateContent</c>. Null
/// optional fields are omitted during serialization.
/// </summary>
internal sealed record GeminiGenerateContentRequest
{
    [JsonPropertyName("contents")]
    public required IReadOnlyList<GeminiContent> Contents { get; init; }

    [JsonPropertyName("systemInstruction")]
    public GeminiContent? SystemInstruction { get; init; }

    [JsonPropertyName("generationConfig")]
    public GeminiGenerationConfig? GenerationConfig { get; init; }
}
