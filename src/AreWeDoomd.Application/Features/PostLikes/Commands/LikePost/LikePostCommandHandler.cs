using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using MediatR;

namespace AreWeDoomd.Application.Features.PostLikes.Commands.LikePost;

public sealed class LikePostCommandHandler(
    IPostRepository postRepository,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : IRequestHandler<LikePostCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(LikePostCommand request, CancellationToken cancellationToken)
    {
        var post = await postRepository.GetByIdWithLikesAsync(request.PostId, cancellationToken);

        if (post is null)
        {
            return Result<bool>.NotFound("post.not_found", "Post not found.");
        }

        var now = dateTimeProvider.UtcNow;
        var liked = post.Like(request.UserId, now);

        if (!liked)
        {
            return Result<bool>.Conflict("post_like.already_liked", "You have already liked this post.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
