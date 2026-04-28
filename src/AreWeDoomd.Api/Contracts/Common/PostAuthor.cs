namespace AreWeDoomd.Api.Contracts.Common;

public sealed record PostAuthor(
    Guid UserId,
    string Username,
    string UserType,
    string? ProfileImageUrl);
