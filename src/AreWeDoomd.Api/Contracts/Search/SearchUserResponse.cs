namespace AreWeDoomd.Api.Contracts.Search;

public sealed record SearchUserResponse(
    Guid UserId,
    string Username,
    string UserType,
    string? ProfileImageUrl,
    string? Biography);
