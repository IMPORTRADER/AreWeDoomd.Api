using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Comments.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Comments.Queries.GetPostComments;

public sealed class GetPostCommentsQueryHandler(
    IPostRepository postRepository,
    ICommentRepository commentRepository)
    : IRequestHandler<GetPostCommentsQuery, Result<CommentListResult>>
{
    private const int GuestCommentsPerPost = 2;
    private const int DefaultCursorPageSize = 30;

    public async Task<Result<CommentListResult>> Handle(
        GetPostCommentsQuery request, CancellationToken cancellationToken)
    {
        var post = await postRepository.GetByIdAsync(request.PostId, cancellationToken);

        if (post is null)
        {
            return Result<CommentListResult>.NotFound("post.not_found", "Post not found.");
        }

        var totalCount = await commentRepository.CountByPostIdAsync(request.PostId, cancellationToken);
        var comments = await GetCommentsAsync(request, cancellationToken);

        var hasMoreBefore = false;
        var hasMoreAfter = false;

        if (comments.Count > 0)
        {
            var first = comments[0];
            var last = comments[^1];
            var (oldest, newest) = first.CreatedAt <= last.CreatedAt ? (first, last) : (last, first);

            hasMoreBefore = await commentRepository.ExistsOlderAsync(
                request.PostId, new CommentCursor(oldest.CreatedAt, oldest.Id), cancellationToken);
            hasMoreAfter = await commentRepository.ExistsNewerAsync(
                request.PostId, new CommentCursor(newest.CreatedAt, newest.Id), cancellationToken);
        }

        return Result<CommentListResult>.Success(
            new CommentListResult(comments, totalCount, hasMoreBefore, hasMoreAfter));
    }

    private async Task<IReadOnlyList<CommentResult>> GetCommentsAsync(
        GetPostCommentsQuery request, CancellationToken cancellationToken)
    {
        var limit = ResolveLimit(request);
        var cursorId = request.Anchor ?? request.Before ?? request.After;

        if (cursorId is not null)
        {
            var cursor = await commentRepository.GetCursorAsync(
                request.PostId, cursorId.Value, cancellationToken);

            if (cursor is not null)
            {
                return await GetCursorPageAsync(
                    request, cursor, limit ?? DefaultCursorPageSize, cancellationToken);
            }
        }

        return await commentRepository.GetPageByPostIdAsync(
            request.PostId,
            limit,
            request.Sort == CommentSortDirection.Desc,
            cancellationToken);
    }

    private async Task<IReadOnlyList<CommentResult>> GetCursorPageAsync(
        GetPostCommentsQuery request,
        CommentCursor cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        if (request.Before is not null)
        {
            return await commentRepository.GetOlderThanAsync(
                request.PostId, cursor, limit, cancellationToken);
        }

        if (request.After is not null)
        {
            return await commentRepository.GetNewerThanAsync(
                request.PostId, cursor, limit, cancellationToken);
        }

        var around = Math.Min(request.Around, Math.Max(0, limit - 1));
        var older = await commentRepository.GetOlderThanAsync(
            request.PostId, cursor, around, cancellationToken);
        var fromAnchor = await commentRepository.GetFromAnchorAsync(
            request.PostId, cursor, limit - older.Count, cancellationToken);

        return older.Concat(fromAnchor).ToList();
    }

    private static int? ResolveLimit(GetPostCommentsQuery request)
    {
        if (request.IncludeAllComments)
        {
            return request.Limit;
        }

        return Math.Min(request.Limit ?? GuestCommentsPerPost, GuestCommentsPerPost);
    }
}
