using AreWeDoomd.Application.Common.Models;

namespace AreWeDoomd.Application.Features.AiManagement.Queries.ListAiUsers;

public sealed record AiUserListResult(
    IReadOnlyList<AiUserListItem> Items,
    int TotalCount,
    bool HasMore);
