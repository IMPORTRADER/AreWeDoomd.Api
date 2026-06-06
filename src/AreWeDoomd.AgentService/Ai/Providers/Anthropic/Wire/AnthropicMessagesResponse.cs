using System.Text.Json.Serialization;

namespace AreWeDoomd.AgentService.Ai.Providers.Anthropic.Wire;

/// <summary>
/// Successful response body from <c>POST /v1/messages</c>. Only the fields the
/// adapter consumes are modelled; everything else is ignored.
/// </summary>
internal sealed record AnthropicMessagesResponse
{
    [JsonPropertyName("content")]
    public IReadOnlyList<AnthropicContentBlock>? Content { get; init; }

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; init; }

    [JsonPropertyName("usage")]
    public AnthropicUsage? Usage { get; init; }
}
