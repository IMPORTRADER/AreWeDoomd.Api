using AreWeDoomd.Application.Features.CommentLikes.Common;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface ICommentLikeRepository
{
    Task<IReadOnlyList<CommentLikeUserResult>> GetLikesByCommentIdAsync(Guid commentId, CancellationToken cancellationToken);
}
