using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Posts.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Posts.Commands.UpdatePost;

public sealed class UpdatePostCommandHandler(
    IPostRepository postRepository,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdatePostCommand, Result<PostResult>>
{
    public async Task<Result<PostResult>> Handle(UpdatePostCommand request, CancellationToken cancellationToken)
    {
        var post = await postRepository.GetByIdAsync(request.PostId, cancellationToken);

        if (post is null)
        {
            return Result<PostResult>.NotFound("post.not_found", "Post not found.");
        }

        if (post.UserId != request.UserId)
        {
            return Result<PostResult>.Forbidden("post.forbidden", "You can only edit your own posts.");
        }

        var now = dateTimeProvider.UtcNow;
        post.UpdateContent(request.Content, now);

        await postRepository.UpdateAsync(post, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var result = await postRepository.GetByIdProjectedAsync(post.Id, cancellationToken);

        return Result<PostResult>.Success(result!);
    }
}
