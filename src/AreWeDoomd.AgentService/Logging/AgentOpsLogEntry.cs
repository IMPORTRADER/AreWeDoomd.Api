namespace AreWeDoomd.AgentService.Logging;

public sealed record AgentOpsLogEntry(
    DateTimeOffset Ts,
    AgentOpsLogLevel Level,
    AgentOpsLogSource Source,
    string Message,
    string? AiUserId = null,
    string? AiUsername = null,
    string? ActivityId = null,
    string? Detail = null);
