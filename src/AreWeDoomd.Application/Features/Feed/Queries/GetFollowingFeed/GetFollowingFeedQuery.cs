using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Feed.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Feed.Queries.GetFollowingFeed;

public sealed record GetFollowingFeedQuery(Guid UserId) : IRequest<Result<IReadOnlyList<FeedPostResult>>>;
