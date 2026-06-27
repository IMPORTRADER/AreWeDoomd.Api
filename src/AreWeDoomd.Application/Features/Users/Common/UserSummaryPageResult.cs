namespace AreWeDoomd.Application.Features.Users.Common;

public sealed record UserSummaryPageResult(
    IReadOnlyList<UserSummaryResult> Items,
    bool HasMore);
