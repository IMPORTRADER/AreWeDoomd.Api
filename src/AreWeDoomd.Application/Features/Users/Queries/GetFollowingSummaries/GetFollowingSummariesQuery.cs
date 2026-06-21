using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Users.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Queries.GetFollowingSummaries;

public sealed record GetFollowingSummariesQuery(
    string Username,
    Guid RequesterId,
    int Offset,
    int PageSize) : IRequest<Result<UserSummaryPageResult>>;
