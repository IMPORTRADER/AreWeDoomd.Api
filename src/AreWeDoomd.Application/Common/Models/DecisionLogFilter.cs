namespace AreWeDoomd.Application.Common.Models;

public sealed record DecisionLogFilter(
    string? AiUserId = null, string? Action = null, string? Outcome = null,
    DateOnly? FromUtc = null, DateOnly? ToUtc = null);
