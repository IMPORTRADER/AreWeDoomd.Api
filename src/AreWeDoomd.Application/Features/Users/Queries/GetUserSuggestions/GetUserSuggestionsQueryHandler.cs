using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Users.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Queries.GetUserSuggestions;

public sealed class GetUserSuggestionsQueryHandler(
    IUserFollowRepository userFollowRepository)
    : IRequestHandler<GetUserSuggestionsQuery, Result<UserSummaryPageResult>>
{
    private const int MaxPageSize = 50;

    public async Task<Result<UserSummaryPageResult>> Handle(
        GetUserSuggestionsQuery request, CancellationToken cancellationToken)
    {
        var offset = Math.Max(0, request.Offset);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var rows = await userFollowRepository.GetSuggestionsAsync(
            request.ViewerId, offset, pageSize + 1, cancellationToken);

        var hasMore = rows.Count > pageSize;
        var page = hasMore ? rows.Take(pageSize).ToList() : rows.ToList();

        return Result<UserSummaryPageResult>.Success(new UserSummaryPageResult(page, hasMore));
    }
}
