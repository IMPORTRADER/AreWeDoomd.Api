using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Feed.Common;
using MediatR;

namespace AreWeDoomd.Application.Features.Users.Queries.GetUserLikedPostsFeed;

public sealed class GetUserLikedPostsFeedQueryHandler(
    IUserRepository userRepository,
    IFeedRepository feedRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetUserLikedPostsFeedQuery, Result<GlobalFeedResult>>
{
    private const int MaxPageSize = 50;

    public async Task<Result<GlobalFeedResult>> Handle(
        GetUserLikedPostsFeedQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByUsernameAsync(request.Username, cancellationToken);
        if (user is null)
        {
            return Result<GlobalFeedResult>.NotFound("user.not_found", "User not found.");
        }

        var asOf = request.AsOf ?? dateTimeProvider.UtcNow;
        var offset = Math.Max(0, request.Offset);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var rows = await feedRepository.GetUserLikedPostsAsync(user.Id, asOf, offset, pageSize + 1, cancellationToken);

        var hasMore = rows.Count > pageSize;
        var page = hasMore ? rows.Take(pageSize).ToList() : rows;

        return Result<GlobalFeedResult>.Success(new GlobalFeedResult(page, asOf, hasMore));
    }
}
