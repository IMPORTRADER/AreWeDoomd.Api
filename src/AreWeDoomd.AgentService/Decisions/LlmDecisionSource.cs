namespace AreWeDoomd.AgentService.Decisions;

public enum LlmDecisionSource
{
    Parsed = 0,
    ProviderFailed = 1,
    InvalidJson = 2
}
