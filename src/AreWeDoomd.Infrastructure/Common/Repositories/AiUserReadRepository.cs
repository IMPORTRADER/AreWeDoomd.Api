using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Domain.Users;
using AreWeDoomd.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AreWeDoomd.Infrastructure.Common.Repositories;

public sealed class AiUserReadRepository(AreWeDoomdDbContext dbContext) : IAiUserReadRepository
{
    public async Task<(IReadOnlyList<AiUserListItem> Items, int TotalCount)> ListAsync(
        string? trait, string? search, string? status, int offset, int pageSize, CancellationToken ct)
    {
        var query = dbContext.Users
            .AsNoTracking()
            .Where(u => u.UserType == UserType.Ai);

        query = status switch
        {
            "persona"     => query.Where(u => u.DeactivatedAt == null && u.AiPersonality != null),
            "noPersona"   => query.Where(u => u.DeactivatedAt == null && u.AiPersonality == null),
            "deactivated" => query.Where(u => u.DeactivatedAt != null),
            _             => query,
        };

        if (!string.IsNullOrWhiteSpace(trait))
        {
            query = query.Where(u =>
                u.AiPersonality != null &&
                EF.Functions.Like(u.AiPersonality.TraitsJson, $"%\"%{trait}%\"%"));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u => EF.Functions.Like(u.Username, $"%{search}%"));
        }

        int totalCount = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip(offset)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.Profile.ProfileImageUrl,
                u.CreatedAt,
                HasPersonality = u.AiPersonality != null,
                TraitsJson = u.AiPersonality != null ? u.AiPersonality.TraitsJson : null,
                TypingStyle = u.AiPersonality != null ? u.AiPersonality.TypingStyle : null,
                PersonaVersion = u.AiPersonality != null ? (int?)u.AiPersonality.Version : null,
                u.DeactivatedAt,
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new AiUserListItem(
            r.Id,
            r.Username,
            r.ProfileImageUrl,
            r.CreatedAt,
            r.HasPersonality,
            r.TraitsJson is not null
                ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(r.TraitsJson) ?? []
                : [],
            r.TypingStyle,
            r.PersonaVersion,
            r.DeactivatedAt))
            .ToList();

        return (items, totalCount);
    }

    public async Task<(int Total, int WithPersonality, int Deactivated)> CountAsync(CancellationToken ct)
    {
        int total = await dbContext.Users
            .Where(u => u.UserType == UserType.Ai).CountAsync(ct);

        int deactivated = await dbContext.Users
            .Where(u => u.UserType == UserType.Ai && u.DeactivatedAt != null).CountAsync(ct);

        int withPersonality = await dbContext.Users
            .Where(u => u.UserType == UserType.Ai && u.DeactivatedAt == null && u.AiPersonality != null)
            .CountAsync(ct);

        return (total, withPersonality, deactivated);
    }
}
