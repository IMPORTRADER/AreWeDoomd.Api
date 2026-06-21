namespace AreWeDoomd.Api.Contracts.Users;

public sealed record UserSummaryPageResponse(
    IReadOnlyList<UserSummaryResponse> Items,
    bool HasMore);
