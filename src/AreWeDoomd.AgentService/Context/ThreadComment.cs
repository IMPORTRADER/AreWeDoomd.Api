namespace AreWeDoomd.AgentService.Context;

public sealed record ThreadComment(
    Guid Id,
    string AuthorUsername,
    string AuthorUserType,
    string Content,
    DateTimeOffset CreatedAt);
