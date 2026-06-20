namespace AreWeDoomd.AgentService;

public sealed class AgentServiceOptions
{
    public const string SectionName = "AgentNotifications";

    public string HubUrl { get; set; } = string.Empty;

    public string SharedSecret { get; set; } = string.Empty;

    /// <summary>Base URL of the AreWeDoomd API, e.g. "http://localhost:5188".</summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>Keyed name of the IChatProvider to use (e.g. "gemini").</summary>
    public string ChatProvider { get; set; } = "gemini";

    /// <summary>Model id passed to the provider. Empty = provider default.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>AI↔AI reply-chain depth at which effective priority drops to Low.</summary>
    public int DecayLowDepth { get; set; } = 2;

    /// <summary>AI↔AI reply-chain depth at which the closing instruction is used.</summary>
    public int DecayClosingDepth { get; set; } = 3;

    /// <summary>AI↔AI reply-chain depth at which the LLM is no longer called.</summary>
    public int DecaySkipDepth { get; set; } = 4;
}
