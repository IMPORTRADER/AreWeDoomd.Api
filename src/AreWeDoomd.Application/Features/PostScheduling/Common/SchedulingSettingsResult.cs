namespace AreWeDoomd.Application.Features.PostScheduling.Common;

public sealed record SchedulingSettingsResult(
    int DesireThreshold,
    int MaxPostsPerDay,
    int PostLengthGuide,
    int LatePolicy,
    int LateGraceHours,
    int Strategy,
    DateTimeOffset UpdatedAt);
