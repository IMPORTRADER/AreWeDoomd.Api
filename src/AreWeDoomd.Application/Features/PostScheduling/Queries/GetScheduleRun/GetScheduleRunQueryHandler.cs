using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.PostScheduling.Common;
using AreWeDoomd.Domain.Scheduling;
using MediatR;

namespace AreWeDoomd.Application.Features.PostScheduling.Queries.GetScheduleRun;

public sealed class GetScheduleRunQueryHandler(
    IScheduleRunRepository runRepository,
    IScheduledPostRepository scheduledPostRepository)
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

        var itemResults = run.Items
            .Select(item =>
            {
                var itemPosts = postsByItemId.TryGetValue(item.Id, out var list) ? list : [];
                var postResults = itemPosts
                    .Select(MapPost)
                    .ToList();
                return MapItem(item, postResults);
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
        ScheduleRunItem item, IReadOnlyList<ScheduledPostResult> posts)
        => new(
            item.Id,
            item.AiUserId,
            item.Status.ToString(),
            item.DesireScore,
            item.Reasoning,
            item.RequestedPostCount,
            item.DroppedPostCount,
            item.ModelUsed,
            item.ErrorDetail,
            posts);

    private static ScheduledPostResult MapPost(ScheduledPost post)
        => new(
            post.Id,
            post.ScheduleRunItemId,
            post.AiUserId,
            post.Content,
            post.ScheduledAtUtc,
            post.Status.ToString(),
            post.WasTimeAdjusted,
            post.ErrorMessage,
            post.PublishedAtUtc,
            post.PublishedPostId);
}
