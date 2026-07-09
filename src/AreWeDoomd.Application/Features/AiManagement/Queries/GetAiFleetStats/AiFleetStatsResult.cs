namespace AreWeDoomd.Application.Features.AiManagement.Queries.GetAiFleetStats;

public sealed record AiFleetStatsResult(
    int TotalAiUsers,
    int WithPersonality,
    int DeactivatedAiUsers,
    int DecisionsToday,
    int ExecutedToday,
    int DroppedToday,
    int FailedToday,
    int ActionsLastHour,
    bool LogAvailable);
