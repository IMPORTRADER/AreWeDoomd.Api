using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Application.Features.PostScheduling.Common;
using AreWeDoomd.Domain.Scheduling;
using MediatR;

namespace AreWeDoomd.Application.Features.PostScheduling.Commands.StartScheduleRun;

public sealed class StartScheduleRunCommandHandler(
    IScheduleRunRepository runRepository,
    IScheduledPostRepository scheduledPostRepository,
    ISchedulingSettingsRepository settingsRepository,
    IScheduleTargetReadRepository targetRepository,
    IScheduleRunHubSender hubSender,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : IRequestHandler<StartScheduleRunCommand, Result<StartScheduleRunResult>>
{
    private static readonly TimeSpan MinWindow = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MinLead = TimeSpan.FromMinutes(5);

    public async Task<Result<StartScheduleRunResult>> Handle(
        StartScheduleRunCommand request, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var runDate = TurkeySchedulingWindow.TurkeyDateOf(now);
        var windowEnd = TurkeySchedulingWindow.DayEndUtc(runDate);
        var windowStart = now + MinLead;

        if (windowEnd - windowStart < MinWindow)
        {
            return Result<StartScheduleRunResult>.Failure(
                "scheduling.window_too_small",
                "Remaining window for today is too small to schedule posts.");
        }

        var targets = await targetRepository.GetTargetsAsync(request.AiUserIds, cancellationToken);
        if (targets.Count == 0)
        {
            return Result<StartScheduleRunResult>.NotFound(
                "scheduling.no_targets", "No matching AI users found.");
        }

        var targetIds = targets.Select(t => t.UserId).ToList();
        var existing = await runRepository.GetActiveItemsForDateAsync(runDate, targetIds, cancellationToken);
        if (existing.Count > 0)
        {
            if (!request.OverwriteExisting)
            {
                var conflictNames = targets
                    .Where(t => existing.Any(e => e.AiUserId == t.UserId))
                    .Select(t => t.Username);
                return Result<StartScheduleRunResult>.Conflict(
                    "scheduling.already_scheduled",
                    $"Already scheduled today: {string.Join(", ", conflictNames)}. Use overwriteExisting to replace.");
            }

            foreach (var item in existing)
            {
                item.MarkSuperseded(now);
            }

            var pendingPosts = await scheduledPostRepository.GetPendingByRunItemIdsAsync(
                existing.Select(e => e.Id).ToList(), cancellationToken);
            foreach (var post in pendingPosts)
            {
                post.Cancel();
            }
        }

        var settings = await settingsRepository.GetAsync(cancellationToken)
            ?? SchedulingSettings.CreateDefault(now);

        var run = ScheduleRun.Create(
            runDate, request.TriggeredByUserId, settings.DesireThreshold,
            settings.MaxPostsPerDay, settings.PostLengthGuide, settings.Strategy, now);
        foreach (var target in targets)
        {
            run.AddItem(target.UserId, now);
        }

        await runRepository.AddAsync(run, cancellationToken);
        var saved = await unitOfWork.TrySaveChangesAsync(cancellationToken);
        if (!saved)
        {
            return Result<StartScheduleRunResult>.Conflict(
                "scheduling.already_scheduled",
                "Another schedule run for one of these accounts was just created. Retry with overwrite if intended.");
        }

        var message = BuildRequest(run, targets, windowStart, windowEnd);
        await hubSender.SendAsync(message, cancellationToken);

        return Result<StartScheduleRunResult>.Success(new StartScheduleRunResult(run.Id, run.Items.Count));
    }

    private static ScheduleRunRequest BuildRequest(
        ScheduleRun run, IReadOnlyList<ScheduleTarget> targets,
        DateTimeOffset windowStart, DateTimeOffset windowEnd)
    {
        var targetByUser = targets.ToDictionary(t => t.UserId);
        var items = run.Items
            .Select(i =>
            {
                var t = targetByUser[i.AiUserId];
                return new ScheduleRunRequestItem(
                    i.Id, i.AiUserId, t.Username, t.PersonaSummary, t.PostsLast3Days, t.LastPostAtUtc);
            })
            .ToList();

        return new ScheduleRunRequest(
            run.Id, run.ThresholdSnapshot, run.MaxPostsSnapshot, run.PostLengthGuideSnapshot,
            (int)run.StrategySnapshot, windowStart, windowEnd, items);
    }
}
