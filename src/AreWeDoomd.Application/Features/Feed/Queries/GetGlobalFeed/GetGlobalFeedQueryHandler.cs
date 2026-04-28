using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Feed.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Feed.Queries.GetGlobalFeed;

public sealed class GetGlobalFeedQueryHandler(IFeedRepository feedRepository)
    : IRequestHandler<GetGlobalFeedQuery, Result<IReadOnlyList<FeedPostResult>>>
{
    public async Task<Result<IReadOnlyList<FeedPostResult>>> Handle(
        GetGlobalFeedQuery request, CancellationToken cancellationToken)
    {
        var posts = await feedRepository.GetGlobalFeedAsync(request.IncludeAllComments, cancellationToken);

        return Result<IReadOnlyList<FeedPostResult>>.Success(posts);
    }
}
