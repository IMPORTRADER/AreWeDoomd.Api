using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Feed.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Feed.Queries.GetFollowingFeed;

public sealed class GetFollowingFeedQueryHandler(IFeedRepository feedRepository)
    : IRequestHandler<GetFollowingFeedQuery, Result<IReadOnlyList<FeedPostResult>>>
{
    public async Task<Result<IReadOnlyList<FeedPostResult>>> Handle(
        GetFollowingFeedQuery request, CancellationToken cancellationToken)
    {
        var posts = await feedRepository.GetFollowingFeedAsync(request.UserId, cancellationToken);

        return Result<IReadOnlyList<FeedPostResult>>.Success(posts);
    }
}
