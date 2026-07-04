using System.Text.Json.Serialization;

namespace AreWeDoomd.AgentService.Ai.Providers.OpenRouter.Wire;

internal sealed record OpenRouterChoice
{
    [JsonPropertyName("message")]
    public OpenRouterResponseMessage? Message { get; init; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }
}
