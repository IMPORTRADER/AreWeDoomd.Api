namespace AreWeDoomd.Application.Common.Models;

public sealed record AgentOpsLogRecord(
    DateTimeOffset Ts, string Level, string Source, string Message,
    string? AiUserId = null, string? AiUsername = null,
    string? ActivityId = null, string? Detail = null,
    int? StatusCode = null);
