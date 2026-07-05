namespace AreWeDoomd.Api.Contracts.PostScheduling;

public sealed record UpdateSchedulingSettingsRequest(
    int DesireThreshold,
    int MaxPostsPerDay,
    int PostLengthGuide,
    int LatePolicy,
    int LateGraceHours,
    int Strategy);
