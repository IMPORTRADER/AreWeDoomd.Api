using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.Comments.Commands.DeleteComment;

public sealed class DeleteCommentCommandHandler(
    IPostRepository postRepository,
    ICommentRepository commentRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteCommentCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeleteCommentCommand request, CancellationToken cancellationToken)
    {
        var post = await postRepository.GetByIdWithCommentsAsync(request.PostId, cancellationToken);

        if (post is null)
        {
            return Result<bool>.NotFound("post.not_found", "Post not found.");
        }

        var comment = post.Comments.FirstOrDefault(x => x.Id == request.CommentId);

        if (comment is null)
        {
            return Result<bool>.NotFound("comment.not_found", "Comment not found.");
        }

        if (comment.UserId != request.UserId)
        {
            return Result<bool>.Forbidden("comment.forbidden", "You can only delete your own comments.");
        }

        post.RemoveComment(request.CommentId);
        await commentRepository.DeleteAsync(comment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
