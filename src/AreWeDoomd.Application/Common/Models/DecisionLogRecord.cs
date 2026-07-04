namespace AreWeDoomd.Application.Common.Models;

public sealed record DecisionLogRecord(
    DateTimeOffset Ts, string AiUserId, string ActivityId, string ActivityType, string Outcome,
    string? Action, string? Reasoning, string? Content, Guid? PostId, Guid? CommentId,
    string? Priority, string? ErrorDetail, int? LlmAttempts, int? PersonaVersion,
    string? PersonaSource, string? SessionLogRef);
