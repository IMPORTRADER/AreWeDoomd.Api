namespace AreWeDoomd.Application.Features.PostScheduling.Common;

public sealed record ScheduleTarget(Guid UserId, string Username, string? PersonaSummary, int PostsLast3Days, DateTimeOffset? LastPostAtUtc);
