using AreWeDoomd.Application.Common.Models;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IAiUserReadRepository
{
    Task<(IReadOnlyList<AiUserListItem> Items, int TotalCount)> ListAsync(
        string? trait, string? search, int offset, int pageSize, CancellationToken ct);

    Task<(int Total, int WithPersonality)> CountAsync(CancellationToken ct);
}
