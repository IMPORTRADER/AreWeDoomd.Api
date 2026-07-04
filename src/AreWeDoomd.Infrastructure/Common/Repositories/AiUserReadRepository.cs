using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Domain.Users;
using AreWeDoomd.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AreWeDoomd.Infrastructure.Common.Repositories;

public sealed class AiUserReadRepository(AreWeDoomdDbContext dbContext) : IAiUserReadRepository
{
    public async Task<(IReadOnlyList<AiUserListItem> Items, int TotalCount)> ListAsync(
        string? trait, string? search, int offset, int pageSize, CancellationToken ct)
    {
        var query = dbContext.Users
            .AsNoTracking()
            .Where(u => u.UserType == UserType.Ai);

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
            r.PersonaVersion))
            .ToList();

        return (items, totalCount);
    }
}
