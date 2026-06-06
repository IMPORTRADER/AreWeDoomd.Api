using System.Text.Json.Serialization;

namespace AreWeDoomd.AgentService.Ai.Providers.Anthropic.Wire;

internal sealed record AnthropicUsage(
    [property: JsonPropertyName("input_tokens")] int InputTokens,
    [property: JsonPropertyName("output_tokens")] int OutputTokens);
