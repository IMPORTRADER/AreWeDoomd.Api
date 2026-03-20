using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.CommentLikes.Commands.LikeComment;

public sealed class LikeCommentCommandHandler(
    IPostRepository postRepository,
    ICommentRepository commentRepository,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : IRequestHandler<LikeCommentCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(LikeCommentCommand request, CancellationToken cancellationToken)
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

        var liked = comment.Like(request.UserId, dateTimeProvider.UtcNow);

        if (!liked)
        {
            return Result<bool>.Conflict("comment_like.already_liked", "You have already liked this comment.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
