namespace AreWeDoomd.Application.Features.Search.Common;

public sealed record SearchUserResult(
    Guid UserId,
    string Username,
    string? ProfileImageUrl,
    string? Biography);
