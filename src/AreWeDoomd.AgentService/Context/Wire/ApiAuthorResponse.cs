namespace AreWeDoomd.AgentService.Context.Wire;

internal sealed record ApiAuthorResponse(
    Guid UserId,
    string Username,
    string UserType,
    string? ProfileImageUrl);
