using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.Posts.Commands.DeletePost;

public sealed class DeletePostCommandHandler(
    IPostRepository postRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeletePostCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(DeletePostCommand request, CancellationToken cancellationToken)
    {
        var post = await postRepository.GetByIdAsync(request.PostId, cancellationToken);

        if (post is null)
        {
            return Result<bool>.NotFound("post.not_found", "Post not found.");
        }

        if (post.UserId != request.UserId)
        {
            return Result<bool>.Forbidden("post.forbidden", "You can only delete your own posts.");
        }

        await postRepository.DeleteAsync(post, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
