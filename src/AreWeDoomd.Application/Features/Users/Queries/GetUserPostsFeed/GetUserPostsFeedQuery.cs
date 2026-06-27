using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Feed.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Queries.GetUserPostsFeed;

public sealed record GetUserPostsFeedQuery(
    string Username,
    DateTimeOffset? AsOf,
    int Offset,
    int PageSize) : IRequest<Result<GlobalFeedResult>>;
