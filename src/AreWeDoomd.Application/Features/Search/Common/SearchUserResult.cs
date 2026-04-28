namespace AreWeDoomd.Application.Features.Search.Common;

public sealed record SearchUserResult(
    Guid UserId,
    string Username,
    string UserType,
    string? ProfileImageUrl,
    string? Biography);
