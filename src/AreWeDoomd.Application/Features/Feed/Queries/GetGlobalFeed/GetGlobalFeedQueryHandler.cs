using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.Feed.Common;
using AreWeDoomd.Application.Features.Feed.Ranking;
using MediatR;

namespace AreWeDoomd.Application.Features.Feed.Queries.GetGlobalFeed;

public sealed class GetGlobalFeedQueryHandler(
    IFeedRepository feedRepository,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<GetGlobalFeedQuery, Result<GlobalFeedResult>>
{
    private const int WindowDays = 30;
    private const int FallbackThreshold = 20;
    private const int MaxCandidates = 1000;
    private const int MaxPageSize = 50;

    public async Task<Result<GlobalFeedResult>> Handle(
        GetGlobalFeedQuery request, CancellationToken cancellationToken)
    {
        var asOf = request.AsOf ?? dateTimeProvider.UtcNow;
        var offset = Math.Max(0, request.Offset);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        // Candidates are the most-recent posts within the window, capped at MaxCandidates.
        // The cap is biased toward recency on purpose: with time-decay ranking, older posts
        // score near the bottom anyway, so excluding the oldest of a very large window does
        // not affect the top of the feed. The fallback (createdAfter: null) is a superset of
        // the windowed set under the same recency ordering, so replacing is safe.
        var candidates = await feedRepository.GetGlobalCandidatesAsync(
            asOf, asOf.AddDays(-WindowDays), MaxCandidates, cancellationToken);

        if (candidates.Count < FallbackThreshold)
        {
            candidates = await feedRepository.GetGlobalCandidatesAsync(
                asOf, null, MaxCandidates, cancellationToken);
        }

        var ranked = FeedScorer.Rank(candidates, asOf);

        var pageSlice = ranked.Skip(offset).Take(pageSize).ToList();
        var hasMore = ranked.Count > offset + pageSize;

        var withComments = await feedRepository.AttachCommentsAsync(
            pageSlice, request.IncludeAllComments, cancellationToken);

        return Result<GlobalFeedResult>.Success(
            new GlobalFeedResult(withComments, asOf, hasMore));
    }
}
