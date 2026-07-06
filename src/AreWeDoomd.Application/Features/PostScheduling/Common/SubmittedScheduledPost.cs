namespace AreWeDoomd.Application.Features.PostScheduling.Common;

public sealed record SubmittedScheduledPost(string Content, DateTimeOffset ScheduledAtUtc);
