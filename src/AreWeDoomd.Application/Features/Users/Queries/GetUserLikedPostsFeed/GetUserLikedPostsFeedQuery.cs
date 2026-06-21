using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Feed.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Queries.GetUserLikedPostsFeed;

public sealed record GetUserLikedPostsFeedQuery(
    string Username,
    DateTimeOffset? AsOf,
    int Offset,
    int PageSize) : IRequest<Result<GlobalFeedResult>>;
