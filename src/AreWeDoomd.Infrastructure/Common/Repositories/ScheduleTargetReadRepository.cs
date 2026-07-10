using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.PostScheduling.Common;
using AreWeDoomd.Domain.Users;
using AreWeDoomd.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AreWeDoomd.Infrastructure.Common.Repositories;

public sealed class ScheduleTargetReadRepository(AreWeDoomdDbContext dbContext) : IScheduleTargetReadRepository
{
    public async Task<IReadOnlyList<ScheduleTarget>> GetTargetsAsync(
        IReadOnlyList<Guid>? aiUserIds, CancellationToken ct)
    {
        var threeDaysAgo = DateTimeOffset.UtcNow.AddDays(-3);

        var query = dbContext.Users.Where(u => u.UserType == UserType.Ai && u.DeactivatedAt == null);
        if (aiUserIds is not null)
        {
            query = query.Where(u => aiUserIds.Contains(u.Id));
        }

        return await query
            .Select(u => new ScheduleTarget(
                u.Id,
                u.Username,
                u.AiPersonality != null ? u.AiPersonality.Summary : null,
                dbContext.Posts.Count(p => p.UserId == u.Id && p.CreatedAt >= threeDaysAgo),
                dbContext.Posts.Where(p => p.UserId == u.Id)
                    .Max(p => (DateTimeOffset?)p.CreatedAt)))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AiUserSummary>> GetUserSummariesAsync(
        IReadOnlyList<Guid> userIds, CancellationToken ct)
    {
        return await dbContext.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new AiUserSummary(u.Id, u.Username, u.Profile.ProfileImageUrl))
            .ToListAsync(ct);
    }
}
