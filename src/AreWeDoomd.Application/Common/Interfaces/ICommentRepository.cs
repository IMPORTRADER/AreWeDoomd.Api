using AreWeDoomd.Application.Features.Comments.Common;
using AreWeDoomd.Domain.Comments;

namespace AreWeDoomd.Application.Common.Interfaces;

public interface ICommentRepository
{
    Task<Comment?> GetByIdAsync(Guid postId, Guid commentId, CancellationToken cancellationToken);
    Task<Comment?> GetByIdWithLikesAsync(Guid postId, Guid commentId, CancellationToken cancellationToken);
    Task<CommentResult?> GetByIdProjectedAsync(Guid postId, Guid commentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CommentResult>> GetByPostIdAsync(Guid postId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CommentResult>> GetByPostIdProjectedAsync(
        Guid postId,
        int? maxComments,
        CancellationToken cancellationToken);
    Task<int> CountByPostIdAsync(Guid postId, CancellationToken cancellationToken);
    Task<CommentCursor?> GetCursorAsync(Guid postId, Guid commentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CommentResult>> GetPageByPostIdAsync(
        Guid postId,
        int? take,
        bool descending,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<CommentResult>> GetOlderThanAsync(
        Guid postId,
        CommentCursor cursor,
        int take,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<CommentResult>> GetFromAnchorAsync(
        Guid postId,
        CommentCursor cursor,
        int take,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<CommentResult>> GetNewerThanAsync(
        Guid postId,
        CommentCursor cursor,
        int take,
        CancellationToken cancellationToken);
    Task<bool> ExistsOlderAsync(Guid postId, CommentCursor cursor, CancellationToken cancellationToken);
    Task<bool> ExistsNewerAsync(Guid postId, CommentCursor cursor, CancellationToken cancellationToken);
    Task AddAsync(Comment comment, CancellationToken cancellationToken);
    Task UpdateAsync(Comment comment, CancellationToken cancellationToken);
    Task DeleteAsync(Comment comment, CancellationToken cancellationToken);
}
