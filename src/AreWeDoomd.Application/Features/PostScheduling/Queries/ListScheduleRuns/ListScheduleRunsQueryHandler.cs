using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.PostScheduling.Common;
using AreWeDoomd.Domain.Scheduling;
using MediatR;

namespace AreWeDoomd.Application.Features.PostScheduling.Queries.ListScheduleRuns;

public sealed class ListScheduleRunsQueryHandler(
    IScheduleRunRepository runRepository,
    IScheduledPostRepository scheduledPostRepository,
    IScheduleTargetReadRepository targetReadRepository)
    : IRequestHandler<ListScheduleRunsQuery, Result<ScheduleRunListResult>>
{
    public async Task<Result<ScheduleRunListResult>> Handle(
        ListScheduleRunsQuery request, CancellationToken cancellationToken)
    {
        var offset = Math.Max(0, request.Offset);
        var pageSize = Math.Clamp(request.PageSize, 1, 50);

        var runs = await runRepository.ListByDateAsync(request.Date, offset, pageSize, cancellationToken);

        var allItemIds = runs.SelectMany(r => r.Items.Select(i => i.Id)).ToList();
        var allPosts = allItemIds.Count > 0
            ? await scheduledPostRepository.GetByRunItemIdsAsync(allItemIds, cancellationToken)
            : [];

        var postsByItemId = allPosts
            .Where(p => p.ScheduleRunItemId.HasValue)
            .GroupBy(p => p.ScheduleRunItemId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var allUserIds = runs.SelectMany(r => r.Items.Select(i => i.AiUserId)).Distinct().ToList();
        var summaries = allUserIds.Count > 0
            ? await targetReadRepository.GetUserSummariesAsync(allUserIds, cancellationToken)
            : [];
        var summaryByUserId = summaries.ToDictionary(s => s.UserId);

        var runResults = runs
            .Select(run =>
            {
                var itemResults = run.Items
                    .Select(item =>
                    {
                        var itemPosts = postsByItemId.TryGetValue(item.Id, out var list) ? list : [];
                        summaryByUserId.TryGetValue(item.AiUserId, out var summary);
                        var postResults = itemPosts.Select(p => MapPost(p, summary)).ToList();
                        return MapItem(item, summary, postResults);
                    })
                    .ToList();
                return MapRun(run, itemResults);
            })
            .ToList();

        return Result<ScheduleRunListResult>.Success(new ScheduleRunListResult(runResults));
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
