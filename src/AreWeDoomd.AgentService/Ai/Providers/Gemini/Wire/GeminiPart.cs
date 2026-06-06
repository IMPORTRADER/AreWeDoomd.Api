using System.Text.Json.Serialization;

namespace AreWeDoomd.AgentService.Ai.Providers.Gemini.Wire;

/// <summary>
/// A single content part. Internal to the Gemini adapter — provider wire shapes
/// never leave this assembly boundary. Only the text part is modelled.
/// </summary>
internal sealed record GeminiPart(
    [property: JsonPropertyName("text")] string? Text);
