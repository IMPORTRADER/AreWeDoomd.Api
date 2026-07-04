namespace AreWeDoomd.Api.Contracts.Admin;

public sealed record AgentDecisionItemResponse(
    DateTimeOffset Ts,
    string AiUserId,
    string ActivityId,
    string ActivityType,
    string Outcome,
    string? Action,
    string? Reasoning,
    string? Content,
    Guid? PostId,
    Guid? CommentId,
    string? Priority,
    string? ErrorDetail,
    int? LlmAttempts,
    int? PersonaVersion,
    string? PersonaSource,
    string? SessionLogRef);
