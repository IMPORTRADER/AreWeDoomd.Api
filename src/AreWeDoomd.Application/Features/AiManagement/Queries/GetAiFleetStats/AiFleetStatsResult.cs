namespace AreWeDoomd.Application.Features.AiManagement.Queries.GetAiFleetStats;

public sealed record AiFleetStatsResult(
    int TotalAiUsers,
    int WithPersonality,
    int DecisionsToday,
    int ExecutedToday,
    int DroppedToday,
    int FailedToday,
    int ActionsLastHour,
    bool LogAvailable);
