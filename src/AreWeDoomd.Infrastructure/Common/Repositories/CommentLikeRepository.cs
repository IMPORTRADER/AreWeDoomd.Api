using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Features.CommentLikes.Common;
using AreWeDoomd.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AreWeDoomd.Infrastructure.Common.Repositories;

public sealed class CommentLikeRepository(AreWeDoomdDbContext dbContext) : ICommentLikeRepository
{
    public async Task<IReadOnlyList<CommentLikeUserResult>> GetLikesByCommentIdAsync(
        Guid commentId, CancellationToken cancellationToken)
    {
        return await (
            from cl in dbContext.CommentLikes
            where cl.CommentId == commentId
            join u in dbContext.Users on cl.UserId equals u.Id
            orderby cl.CreatedAt descending
            select new CommentLikeUserResult(
                u.Id,
                u.Username,
                u.Profile.ProfileImageUrl,
                cl.CreatedAt)
        ).ToListAsync(cancellationToken);
    }
}
