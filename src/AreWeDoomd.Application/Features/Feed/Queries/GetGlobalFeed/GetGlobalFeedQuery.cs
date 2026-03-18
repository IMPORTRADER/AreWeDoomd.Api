using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Posts.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Feed.Queries.GetGlobalFeed;

public sealed record GetGlobalFeedQuery() : IRequest<Result<IReadOnlyList<PostResult>>>;
