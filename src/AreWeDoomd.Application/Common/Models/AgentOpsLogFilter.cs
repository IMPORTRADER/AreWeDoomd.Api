namespace AreWeDoomd.Application.Common.Models;

public sealed record AgentOpsLogFilter(
    string? Level = null, string? Source = null, string? AiUserId = null,
    DateOnly? FromUtc = null, DateOnly? ToUtc = null);
