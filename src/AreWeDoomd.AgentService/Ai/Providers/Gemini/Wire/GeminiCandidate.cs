using System.Text.Json.Serialization;

namespace AreWeDoomd.AgentService.Ai.Providers.Gemini.Wire;

internal sealed record GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; init; }

    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; init; }
}
