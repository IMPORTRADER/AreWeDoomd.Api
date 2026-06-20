using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Feed.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Feed.Queries.GetGlobalFeed;

public sealed record GetGlobalFeedQuery(
    bool IncludeAllComments,
    DateTimeOffset? AsOf,
    int Offset,
    int PageSize) : IRequest<Result<GlobalFeedResult>>;
