using System.Text.Json.Serialization;

namespace AreWeDoomd.ChatProviders.Providers.Gemini.Wire;

/// <summary>
/// Successful response body from <c>generateContent</c>. Only the fields the
/// adapter consumes are modelled; everything else is ignored.
/// </summary>
internal sealed record GeminiGenerateContentResponse
{
    [JsonPropertyName("candidates")]
    public IReadOnlyList<GeminiCandidate>? Candidates { get; init; }

    [JsonPropertyName("usageMetadata")]
    public GeminiUsageMetadata? UsageMetadata { get; init; }
}
