namespace AreWeDoomd.Api.Contracts.PostScheduling;

public sealed record SchedulingSettingsResponse(
    int DesireThreshold,
    int MaxPostsPerDay,
    int PostLengthGuide,
    int LatePolicy,
    int LateGraceHours,
    int Strategy,
    DateTimeOffset UpdatedAt);
