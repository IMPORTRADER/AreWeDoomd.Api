using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.CommentLikes.Commands.UnlikeComment;

public sealed class UnlikeCommentCommandHandler(
    IPostRepository postRepository,
    ICommentRepository commentRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UnlikeCommentCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UnlikeCommentCommand request, CancellationToken cancellationToken)
    {
        var post = await postRepository.GetByIdAsync(request.PostId, cancellationToken);

        if (post is null)
        {
            return Result<bool>.NotFound("post.not_found", "Post not found.");
        }

        var comment = await commentRepository.GetByIdWithLikesAsync(
            request.PostId,
            request.CommentId,
            cancellationToken);

        if (comment is null)
        {
            return Result<bool>.NotFound("comment.not_found", "Comment not found.");
        }

        var unliked = comment.Unlike(request.UserId);

        if (!unliked)
        {
            return Result<bool>.NotFound("comment_like.not_found", "You have not liked this comment.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
