namespace AreWeDoomd.Api.Contracts.Users;

public sealed record ProfileStatsResponse(
    int PostCount,
    int LikeCount,
    int CommentCount,
    int FollowerCount,
    int FollowingCount);
