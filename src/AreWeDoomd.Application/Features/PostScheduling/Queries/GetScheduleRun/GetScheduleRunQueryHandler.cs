using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.PostScheduling.Common;
using AreWeDoomd.Domain.Scheduling;
using MediatR;

namespace AreWeDoomd.Application.Features.PostScheduling.Queries.GetScheduleRun;

public sealed class GetScheduleRunQueryHandler(
    IScheduleRunRepository runRepository,
    IScheduledPostRepository scheduledPostRepository,
    IScheduleTargetReadRepository targetReadRepository)
    : IRequestHandler<GetScheduleRunQuery, Result<ScheduleRunDetailResult>>
{
    public async Task<Result<ScheduleRunDetailResult>> Handle(
        GetScheduleRunQuery request, CancellationToken cancellationToken)
    {
        var run = await runRepository.GetWithItemsAsync(request.RunId, cancellationToken);
        if (run is null)
        {
            return Result<ScheduleRunDetailResult>.NotFound("scheduling.run_not_found", "Schedule run not found.");
        }

        var itemIds = run.Items.Select(i => i.Id).ToList();
        var posts = await scheduledPostRepository.GetByRunItemIdsAsync(itemIds, cancellationToken);
        var postsByItemId = posts
            .Where(p => p.ScheduleRunItemId.HasValue)
            .GroupBy(p => p.ScheduleRunItemId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var allUserIds = run.Items.Select(i => i.AiUserId).Distinct().ToList();
        var summaries = await targetReadRepository.GetUserSummariesAsync(allUserIds, cancellationToken);
        var summaryByUserId = summaries.ToDictionary(s => s.UserId);

        var itemResults = run.Items
            .Select(item =>
            {
                var itemPosts = postsByItemId.TryGetValue(item.Id, out var list) ? list : [];
                summaryByUserId.TryGetValue(item.AiUserId, out var summary);
                var postResults = itemPosts
                    .Select(p => MapPost(p, summary))
                    .ToList();
                return MapItem(item, summary, postResults);
            })
            .ToList();

        return Result<ScheduleRunDetailResult>.Success(MapRun(run, itemResults));
    }

    private static ScheduleRunDetailResult MapRun(
        ScheduleRun run, IReadOnlyList<ScheduleRunItemResult> items)
        => new(
            run.Id,
            run.RunDate,
            run.ThresholdSnapshot,
            run.MaxPostsSnapshot,
            run.Status.ToString(),
            run.CreatedAt,
            run.CompletedAt,
            items);

    private static ScheduleRunItemResult MapItem(
        ScheduleRunItem item, AiUserSummary? summary, IReadOnlyList<ScheduledPostResult> posts)
        => new(
            item.Id,
            item.AiUserId,
            summary?.Username ?? "unknown",
            summary?.ProfileImageUrl,
            item.Status.ToString(),
            item.DesireScore,
            item.Reasoning,
            item.RequestedPostCount,
            item.DroppedPostCount,
            item.ModelUsed,
            item.ErrorDetail,
            posts);

    private static ScheduledPostResult MapPost(ScheduledPost post, AiUserSummary? summary)
        => new(
            post.Id,
            post.ScheduleRunItemId,
            post.AiUserId,
            summary?.Username ?? "unknown",
            summary?.ProfileImageUrl,
            post.Content,
            post.ScheduledAtUtc,
            post.Status.ToString(),
            post.WasTimeAdjusted,
            post.ErrorMessage,
            post.PublishedAtUtc,
            post.PublishedPostId);
}
