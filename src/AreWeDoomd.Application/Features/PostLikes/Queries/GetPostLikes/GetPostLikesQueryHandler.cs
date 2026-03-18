using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.PostLikes.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.PostLikes.Queries.GetPostLikes;

public sealed class GetPostLikesQueryHandler(
    IPostRepository postRepository,
    IPostLikeRepository postLikeRepository)
    : IRequestHandler<GetPostLikesQuery, Result<IReadOnlyList<PostLikeUserResult>>>
{
    public async Task<Result<IReadOnlyList<PostLikeUserResult>>> Handle(
        GetPostLikesQuery request, CancellationToken cancellationToken)
    {
        var post = await postRepository.GetByIdAsync(request.PostId, cancellationToken);

        if (post is null)
        {
            return Result<IReadOnlyList<PostLikeUserResult>>.NotFound(
                "post.not_found", "Post not found.");
        }

        var likes = await postLikeRepository.GetLikesByPostIdAsync(request.PostId, cancellationToken);

        return Result<IReadOnlyList<PostLikeUserResult>>.Success(likes);
    }
}
