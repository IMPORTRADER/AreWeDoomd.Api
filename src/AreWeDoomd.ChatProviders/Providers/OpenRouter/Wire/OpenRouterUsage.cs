using System.Text.Json.Serialization;

namespace AreWeDoomd.AgentService.Ai.Providers.OpenRouter.Wire;

internal sealed record OpenRouterUsage(
    [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
    [property: JsonPropertyName("completion_tokens")] int CompletionTokens);
