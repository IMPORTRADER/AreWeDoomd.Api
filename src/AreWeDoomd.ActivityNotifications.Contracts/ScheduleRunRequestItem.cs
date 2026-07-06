namespace AreWeDoomd.ActivityNotifications.Contracts;

public sealed record ScheduleRunRequestItem(
    Guid RunItemId,
    Guid AiUserId,
    string Username,
    string? PersonaSummary,
    int PostsLast3Days,
    DateTimeOffset? LastPostAtUtc);
