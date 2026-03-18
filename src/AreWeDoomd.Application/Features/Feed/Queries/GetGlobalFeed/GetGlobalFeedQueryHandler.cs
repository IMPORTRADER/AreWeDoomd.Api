using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Posts.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Feed.Queries.GetGlobalFeed;

public sealed class GetGlobalFeedQueryHandler(IFeedRepository feedRepository)
    : IRequestHandler<GetGlobalFeedQuery, Result<IReadOnlyList<PostResult>>>
{
    public async Task<Result<IReadOnlyList<PostResult>>> Handle(
        GetGlobalFeedQuery request, CancellationToken cancellationToken)
    {
        var posts = await feedRepository.GetGlobalFeedAsync(cancellationToken);

        return Result<IReadOnlyList<PostResult>>.Success(posts);
    }
}
