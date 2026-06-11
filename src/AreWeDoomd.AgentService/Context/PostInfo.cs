namespace AreWeDoomd.AgentService.Context;

public sealed record PostInfo(
    Guid Id,
    string AuthorUsername,
    string AuthorUserType,
    string Content);
