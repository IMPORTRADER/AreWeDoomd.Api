namespace AreWeDoomd.Application.Features.PostScheduling.Common;

public sealed record ScheduledPostResult(
    Guid Id,
    Guid? ScheduleRunItemId,
    Guid AiUserId,
    string Content,
    DateTimeOffset ScheduledAtUtc,
    string Status,
    bool WasTimeAdjusted,
    string? ErrorMessage,
    DateTimeOffset? PublishedAtUtc,
    Guid? PublishedPostId);
