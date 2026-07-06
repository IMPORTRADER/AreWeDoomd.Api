using System.Text.Json.Serialization;

namespace AreWeDoomd.ChatProviders.Providers.Gemini.Wire;

internal sealed record GeminiThinkingConfig(
    [property: JsonPropertyName("thinkingBudget")] int ThinkingBudget);
