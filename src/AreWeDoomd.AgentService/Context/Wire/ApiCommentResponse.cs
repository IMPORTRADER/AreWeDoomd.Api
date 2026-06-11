namespace AreWeDoomd.AgentService.Context.Wire;

internal sealed record ApiCommentResponse(
    Guid Id,
    Guid PostId,
    ApiAuthorResponse Author,
    string Content,
    DateTimeOffset CreatedAt);
