using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Domain.Posts;
using AreWeDoomd.Domain.Scheduling;
using MediatR;

namespace AreWeDoomd.Application.Features.PostScheduling.Commands.ProcessDueScheduledPosts;

public sealed class ProcessDueScheduledPostsCommandHandler(
    IScheduledPostRepository scheduledPostRepository,
    IPostRepository postRepository,
    ISchedulingSettingsRepository settingsRepository,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork,
    IAgentOpsLogger opsLog)
    : IRequestHandler<ProcessDueScheduledPostsCommand, Result<DateTimeOffset?>>
{
    private static readonly TimeSpan StuckThreshold = TimeSpan.FromMinutes(5);

    public async Task<Result<DateTimeOffset?>> Handle(
        ProcessDueScheduledPostsCommand request, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var settings = await settingsRepository.GetAsync(cancellationToken)
            ?? SchedulingSettings.CreateDefault(now);

        // 1) Stuck-Publishing kurtarma: Post zaten yaratıldıysa Published'a tamamla,
        //    yaratılmadıysa güvenle yeniden dene (PublishedPostId sabit → duplikasyon imkânsız).
        var stuck = await scheduledPostRepository.GetStuckPublishingAsync(now - StuckThreshold, cancellationToken);
        foreach (var post in stuck)
        {
            var existing = await postRepository.GetByIdAsync(post.PublishedPostId!.Value, cancellationToken);
            if (existing is not null)
            {
                post.MarkPublished(now);
            }
            else
            {
                await PublishAsync(post, now, cancellationToken);
            }
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // 2) Due Pending gönderiler
        var due = await scheduledPostRepository.GetDuePendingAsync(now, cancellationToken);
        foreach (var post in due)
        {
            var lateness = now - post.ScheduledAtUtc;
            bool beyondGrace = lateness > TimeSpan.FromHours(settings.LateGraceHours);
            bool beyondTurkeyDay = TurkeySchedulingWindow.TurkeyDateOf(post.ScheduledAtUtc)
                != TurkeySchedulingWindow.TurkeyDateOf(now);

            if ((beyondGrace || beyondTurkeyDay) && settings.LatePolicy == SchedulingLatePolicy.Expire)
            {
                post.Expire();
                opsLog.TryLog(new AgentOpsLogRecord(
                    now, AgentOpsLogLevels.Warning, AgentOpsLogSources.Scheduling,
                    $"Scheduled post expired (late beyond policy) for AI user {post.AiUserId}.",
                    AiUserId: post.AiUserId.ToString()));
                await unitOfWork.SaveChangesAsync(cancellationToken);
                continue;
            }

            var claimToken = Guid.NewGuid();
            var newPostId = Guid.NewGuid();
            bool claimed = await scheduledPostRepository.TryClaimAsync(
                post.Id, claimToken, newPostId, cancellationToken);
            if (!claimed)
            {
                // EnableRetryOnFailure altında UPDATE gitti ama sonuç kaybolduysa
                // rowcount 0 görünür; token geri-okumasıyla ayırt et.
                claimed = await scheduledPostRepository.WasClaimWonAsync(post.Id, claimToken, cancellationToken);
            }
            if (!claimed)
            {
                continue; // başka instance kazandı ya da durum değişti
            }

            // Claim ExecuteUpdate ile DB'ye yazıldı; in-memory entity'yi senkronla.
            var fresh = await scheduledPostRepository.GetByIdAsync(post.Id, cancellationToken);
            if (fresh is null || fresh.Status == ScheduledPostStatus.Pending)
            {
                post.BeginPublishing(claimToken, newPostId); // in-memory senkron (unit test yolu)
                fresh = post;
            }
            await PublishAsync(fresh, now, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var next = await scheduledPostRepository.GetNextPendingDueUtcAsync(cancellationToken);
        return Result<DateTimeOffset?>.Success(next);
    }

    private async Task PublishAsync(ScheduledPost scheduledPost, DateTimeOffset now, CancellationToken ct)
    {
        try
        {
            // Post + ScheduledPost güncellemesi aynı DbContext'te → tek SaveChanges = tek transaction.
            var post = Post.CreateWithId(
                scheduledPost.PublishedPostId!.Value, scheduledPost.AiUserId, scheduledPost.Content, now);
            await postRepository.AddAsync(post, ct);
            scheduledPost.MarkPublished(now);
            opsLog.TryLog(new AgentOpsLogRecord(
                now, AgentOpsLogLevels.Info, AgentOpsLogSources.Scheduling,
                $"Scheduled post published for AI user {scheduledPost.AiUserId}.",
                AiUserId: scheduledPost.AiUserId.ToString()));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            scheduledPost.MarkFailed(ex.Message);
            opsLog.TryLog(new AgentOpsLogRecord(
                now, AgentOpsLogLevels.Error, AgentOpsLogSources.Scheduling,
                $"Scheduled post publish failed for AI user {scheduledPost.AiUserId}.",
                AiUserId: scheduledPost.AiUserId.ToString(), Detail: ex.Message));
        }
    }
}
