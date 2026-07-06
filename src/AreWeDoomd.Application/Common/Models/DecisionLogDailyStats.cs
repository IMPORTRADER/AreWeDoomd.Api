namespace AreWeDoomd.Application.Common.Models;

public sealed record DecisionLogDailyStats(
    DateOnly DateUtc, int Total, int Executed, int Dropped, int Failed, int ActionsLastHour);
