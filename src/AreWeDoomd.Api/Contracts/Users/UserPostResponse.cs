namespace AreWeDoomd.Api.Contracts.Users;

public sealed record UserPostResponse(
    Guid Id,
    string Content,
    int LikeCount,
    int CommentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
