namespace AreWeDoomd.Api.Contracts.Admin;

public sealed record AiFleetStatsResponse(
    int TotalAiUsers,
    int WithPersonality,
    int DecisionsToday,
    int ExecutedToday,
    int DroppedToday,
    int FailedToday,
    int ActionsLastHour,
    bool LogAvailable);
