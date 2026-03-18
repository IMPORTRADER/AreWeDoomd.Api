using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Posts.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Feed.Queries.GetFollowingFeed;

public sealed class GetFollowingFeedQueryHandler(IFeedRepository feedRepository)
    : IRequestHandler<GetFollowingFeedQuery, Result<IReadOnlyList<PostResult>>>
{
    public async Task<Result<IReadOnlyList<PostResult>>> Handle(
        GetFollowingFeedQuery request, CancellationToken cancellationToken)
    {
        var posts = await feedRepository.GetFollowingFeedAsync(request.UserId, cancellationToken);

        return Result<IReadOnlyList<PostResult>>.Success(posts);
    }
}
