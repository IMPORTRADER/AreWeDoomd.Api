namespace AreWeDoomd.Api.Contracts.Admin;

public sealed record AgentOpsLogItemResponse(
    DateTimeOffset Ts,
    string Level,
    string Source,
    string Message,
    string? AiUserId,
    string? AiUsername,
    string? ActivityId,
    string? Detail);
