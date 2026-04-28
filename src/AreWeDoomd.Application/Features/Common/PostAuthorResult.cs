namespace AreWeDoomd.Application.Features.Common;

public sealed record PostAuthorResult(
    Guid UserId,
    string Username,
    string UserType,
    string? ProfileImageUrl);
