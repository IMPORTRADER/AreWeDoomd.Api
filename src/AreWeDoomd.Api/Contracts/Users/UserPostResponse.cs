namespace AreWeDoomd.Api.Contracts.Users;

public sealed record UserPostResponse(
    Guid Id,
    string UserType,
    string Content,
    int LikeCount,
    int CommentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
