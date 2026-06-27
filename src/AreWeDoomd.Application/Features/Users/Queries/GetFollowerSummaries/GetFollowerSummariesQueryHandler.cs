using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Users.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Queries.GetFollowerSummaries;

public sealed class GetFollowerSummariesQueryHandler(
    IUserRepository userRepository,
    IUserFollowRepository userFollowRepository)
    : IRequestHandler<GetFollowerSummariesQuery, Result<UserSummaryPageResult>>
{
    private const int MaxPageSize = 50;

    public async Task<Result<UserSummaryPageResult>> Handle(
        GetFollowerSummariesQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByUsernameAsync(request.Username, cancellationToken);
        if (user is null)
        {
            return Result<UserSummaryPageResult>.NotFound("user.not_found", "User not found.");
        }

        var offset = Math.Max(0, request.Offset);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var rows = await userFollowRepository.GetFollowerSummariesAsync(
            user.Id, request.RequesterId, offset, pageSize + 1, cancellationToken);

        var hasMore = rows.Count > pageSize;
        var page = hasMore ? rows.Take(pageSize).ToList() : rows.ToList();

        return Result<UserSummaryPageResult>.Success(new UserSummaryPageResult(page, hasMore));
    }
}
