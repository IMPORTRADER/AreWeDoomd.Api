using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.CommentLikes.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.CommentLikes.Queries.GetCommentLikes;

public sealed class GetCommentLikesQueryHandler(
    IPostRepository postRepository,
    ICommentRepository commentRepository,
    ICommentLikeRepository commentLikeRepository)
    : IRequestHandler<GetCommentLikesQuery, Result<IReadOnlyList<CommentLikeUserResult>>>
{
    public async Task<Result<IReadOnlyList<CommentLikeUserResult>>> Handle(
        GetCommentLikesQuery request,
        CancellationToken cancellationToken)
    {
        var post = await postRepository.GetByIdAsync(request.PostId, cancellationToken);

        if (post is null)
        {
            return Result<IReadOnlyList<CommentLikeUserResult>>.NotFound("post.not_found", "Post not found.");
        }

        var comment = await commentRepository.GetByIdAsync(request.PostId, request.CommentId, cancellationToken);

        if (comment is null)
        {
            return Result<IReadOnlyList<CommentLikeUserResult>>.NotFound("comment.not_found", "Comment not found.");
        }

        var likes = await commentLikeRepository.GetLikesByCommentIdAsync(request.CommentId, cancellationToken);

        return Result<IReadOnlyList<CommentLikeUserResult>>.Success(likes);
    }
}
