namespace AreWeDoomd.Api.Contracts.Users;

public sealed record FollowStateResponse(
    bool IsFollowedByMe,
    int FollowerCount);
