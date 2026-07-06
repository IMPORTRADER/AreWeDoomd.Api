namespace AreWeDoomd.AgentService.Logging;

public enum DecisionOutcome
{
    Executed = 0,
    SkippedPriority = 1,
    Ignored = 2,
    LlmFailed = 3,
    LlmFallback = 4,
    ActionFailed = 5,
    Dropped = 6
}
