using AreWeDoomd.Application.Features.Comments.Common;
using AreWeDoomd.Domain.Comments;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface ICommentRepository
{
    Task<Comment?> GetByIdAsync(Guid postId, Guid commentId, CancellationToken cancellationToken);
    Task<Comment?> GetByIdWithLikesAsync(Guid postId, Guid commentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CommentResult>> GetByPostIdAsync(Guid postId, CancellationToken cancellationToken);
    Task AddAsync(Comment comment, CancellationToken cancellationToken);
    Task UpdateAsync(Comment comment, CancellationToken cancellationToken);
    Task DeleteAsync(Comment comment, CancellationToken cancellationToken);
}
