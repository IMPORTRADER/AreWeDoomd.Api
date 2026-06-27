namespace AreWeDoomd.Application.Features.Users.Common;

public sealed record FollowStateResult(
    bool IsFollowedByMe,
    int FollowerCount);
