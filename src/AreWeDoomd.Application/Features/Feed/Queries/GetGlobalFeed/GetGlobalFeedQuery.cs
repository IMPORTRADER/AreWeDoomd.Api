using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Feed.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Feed.Queries.GetGlobalFeed;

public sealed record GetGlobalFeedQuery(bool IncludeAllComments) : IRequest<Result<IReadOnlyList<FeedPostResult>>>;
