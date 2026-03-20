using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Comments.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Comments.Commands.UpdateComment;

public sealed class UpdateCommentCommandHandler(
    IPostRepository postRepository,
    ICommentRepository commentRepository,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateCommentCommand, Result<CommentResult>>
{
    public async Task<Result<CommentResult>> Handle(UpdateCommentCommand request, CancellationToken cancellationToken)
    {
        var post = await postRepository.GetByIdAsync(request.PostId, cancellationToken);

        if (post is null)
        {
            return Result<CommentResult>.NotFound("post.not_found", "Post not found.");
        }

        var comment = await commentRepository.GetByIdAsync(request.PostId, request.CommentId, cancellationToken);

        if (comment is null)
        {
            return Result<CommentResult>.NotFound("comment.not_found", "Comment not found.");
        }

        if (comment.UserId != request.UserId)
        {
            return Result<CommentResult>.Forbidden("comment.forbidden", "You can only edit your own comments.");
        }

        comment.UpdateContent(request.Content, dateTimeProvider.UtcNow);

        await commentRepository.UpdateAsync(comment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CommentResult>.Success(
            new CommentResult(
                comment.Id,
                comment.PostId,
                comment.UserId,
                comment.Content,
                comment.CreatedAt,
                comment.UpdatedAt));
    }
}
