using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Comments.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Comments.Commands.CreateComment;

public sealed class CreateCommentCommandHandler(
    IPostRepository postRepository,
    ICommentRepository commentRepository,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateCommentCommand, Result<CommentResult>>
{
    public async Task<Result<CommentResult>> Handle(CreateCommentCommand request, CancellationToken cancellationToken)
    {
        var post = await postRepository.GetByIdAsync(request.PostId, cancellationToken);

        if (post is null)
        {
            return Result<CommentResult>.NotFound("post.not_found", "Post not found.");
        }

        var now = dateTimeProvider.UtcNow;
        var comment = post.AddComment(request.UserId, request.Content, now);

        await commentRepository.AddAsync(comment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CommentResult>.Success(
            new CommentResult(
                comment.Id,
                comment.PostId,
                comment.UserId,
                comment.Content,
                comment.LikeCount,
                comment.CreatedAt,
                comment.UpdatedAt));
    }
}
