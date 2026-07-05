using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.PostScheduling.Common;
using AreWeDoomd.Domain.Scheduling;
using MediatR;

namespace AreWeDoomd.Application.Features.PostScheduling.Queries.ListScheduledPosts;

public sealed class ListScheduledPostsQueryHandler(
    IScheduledPostRepository repository)
    : IRequestHandler<ListScheduledPostsQuery, Result<ScheduledPostListResult>>
{
    public async Task<Result<ScheduledPostListResult>> Handle(
        ListScheduledPostsQuery request, CancellationToken cancellationToken)
    {
        var from = TurkeySchedulingWindow.DayStartUtc(request.Date);
        var to = TurkeySchedulingWindow.DayEndUtc(request.Date);

        ScheduledPostStatus? status = request.Status.HasValue
            ? (ScheduledPostStatus)request.Status.Value
            : null;

        var posts = await repository.ListAsync(from, to, request.AiUserId, status, cancellationToken);

        var results = posts
            .Select(p => new ScheduledPostResult(
                p.Id,
                p.ScheduleRunItemId,
                p.AiUserId,
                p.Content,
                p.ScheduledAtUtc,
                p.Status.ToString(),
                p.WasTimeAdjusted,
                p.ErrorMessage,
                p.PublishedAtUtc,
                p.PublishedPostId))
            .ToList();

        return Result<ScheduledPostListResult>.Success(new ScheduledPostListResult(results));
    }
}
