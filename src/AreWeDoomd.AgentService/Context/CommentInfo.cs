namespace AreWeDoomd.AgentService.Context;

public sealed record CommentInfo(
    Guid Id,
    string AuthorUsername,
    string AuthorUserType,
    string Content,
    DateTimeOffset CreatedAt);
