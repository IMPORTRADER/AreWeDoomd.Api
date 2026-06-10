namespace AreWeDoomd.AgentService.Context.Wire;

internal sealed record ApiPostResponse(
    Guid Id,
    ApiAuthorResponse Author,
    string Content);
