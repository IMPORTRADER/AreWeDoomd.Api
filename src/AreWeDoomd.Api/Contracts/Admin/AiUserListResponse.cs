namespace AreWeDoomd.Api.Contracts.Admin;

public sealed record AiUserListResponse(
    IReadOnlyList<AiUserItemResponse> Items,
    int TotalCount,
    bool HasMore);
