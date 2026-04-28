using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Comments.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Comments.Queries.GetPostComments;

public sealed class GetPostCommentsQueryHandler(
    IPostRepository postRepository,
    ICommentRepository commentRepository)
    : IRequestHandler<GetPostCommentsQuery, Result<IReadOnlyList<CommentResult>>>
{
    private const int GuestCommentsPerPost = 2;

    public async Task<Result<IReadOnlyList<CommentResult>>> Handle(
        GetPostCommentsQuery request, CancellationToken cancellationToken)
    {
        var post = await postRepository.GetByIdAsync(request.PostId, cancellationToken);

        if (post is null)
        {
            return Result<IReadOnlyList<CommentResult>>.NotFound("post.not_found", "Post not found.");
        }

        var comments = await commentRepository.GetByPostIdProjectedAsync(
            request.PostId,
            request.IncludeAllComments ? null : GuestCommentsPerPost,
            cancellationToken);

        return Result<IReadOnlyList<CommentResult>>.Success(comments);
    }
}
