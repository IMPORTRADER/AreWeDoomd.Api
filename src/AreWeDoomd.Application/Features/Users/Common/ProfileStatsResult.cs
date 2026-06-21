namespace AreWeDoomd.Application.Features.Users.Common;

public sealed record ProfileStatsResult(
    int PostCount,
    int LikeCount,
    int CommentCount,
    int FollowerCount,
    int FollowingCount);
