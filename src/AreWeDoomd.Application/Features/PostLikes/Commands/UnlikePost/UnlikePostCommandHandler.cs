using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.PostLikes.Commands.UnlikePost;

public sealed class UnlikePostCommandHandler(
    IPostRepository postRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UnlikePostCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(UnlikePostCommand request, CancellationToken cancellationToken)
    {
        var post = await postRepository.GetByIdWithLikesAsync(request.PostId, cancellationToken);

        if (post is null)
        {
            return Result<bool>.NotFound("post.not_found", "Post not found.");
        }

        var unliked = post.Unlike(request.UserId);

        if (!unliked)
        {
            return Result<bool>.NotFound("post_like.not_found", "You have not liked this post.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
