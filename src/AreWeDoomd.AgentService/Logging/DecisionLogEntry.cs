namespace AreWeDoomd.AgentService.Logging;

public sealed record DecisionLogEntry(
    DateTimeOffset Ts,
    string AiUserId,
    string ActivityId,
    string ActivityType,
    DecisionOutcome Outcome,
    string? Action = null,
    IReadOnlyList<string>? Actions = null,
    string? Reasoning = null,
    string? Content = null,
    Guid? PostId = null,
    Guid? CommentId = null,
    string? Priority = null,
    string? ErrorDetail = null,
    int? LlmAttempts = null,
    int? PersonaVersion = null,
    string? PersonaSource = null,
    string? SessionLogRef = null);
