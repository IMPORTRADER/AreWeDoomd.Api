namespace AreWeDoomd.Application.Features.Users.Common;

public sealed record UserPostResult(
    Guid Id,
    string Content,
    int LikeCount,
    int CommentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
