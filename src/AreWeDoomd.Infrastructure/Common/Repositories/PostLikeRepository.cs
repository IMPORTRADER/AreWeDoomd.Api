using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.PostLikes.Common;
using AreWeDoomd.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AreWeDoomd.Infrastructure.Common.Repositories;

public sealed class PostLikeRepository(AreWeDoomdDbContext dbContext) : IPostLikeRepository
{
    public async Task<IReadOnlyList<PostLikeUserResult>> GetLikesByPostIdAsync(
        Guid postId, CancellationToken cancellationToken)
    {
        return await (
            from pl in dbContext.PostLikes
            where pl.PostId == postId
            join u in dbContext.Users on pl.UserId equals u.Id
            orderby pl.CreatedAt descending
            select new PostLikeUserResult(
                u.Id,
                u.Username,
                u.UserType.ToString(),
                u.Profile.ProfileImageUrl,
                pl.CreatedAt)
            ).ToListAsync(cancellationToken);
    }
}
