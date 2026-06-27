namespace AreWeDoomd.Api.Contracts.Users;

public sealed record ProfileBadgeResponse(
    string Code,
    string Label,
    string Description);
