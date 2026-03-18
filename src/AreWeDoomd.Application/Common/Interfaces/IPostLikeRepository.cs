using AreWeDoomd.Application.Features.PostLikes.Common;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface IPostLikeRepository
{
    Task<IReadOnlyList<PostLikeUserResult>> GetLikesByPostIdAsync(Guid postId, CancellationToken cancellationToken);
}
