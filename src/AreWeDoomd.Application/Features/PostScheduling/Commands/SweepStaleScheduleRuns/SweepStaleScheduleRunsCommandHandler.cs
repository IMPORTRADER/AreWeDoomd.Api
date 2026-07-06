using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Domain.Scheduling;
using MediatR;
using DomainLlmSettings = AreWeDoomd.Domain.Ai.LlmSettings;

namespace AreWeDoomd.Application.Features.PostScheduling.Commands.SweepStaleScheduleRuns;

public sealed class SweepStaleScheduleRunsCommandHandler(
    IScheduleRunRepository runRepository,
    IScheduleTargetReadRepository targetRepository,
    IScheduleRunHubSender hubSender,
    ILlmSettingsRepository llmSettingsRepository,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : IRequestHandler<SweepStaleScheduleRunsCommand, Result<Unit>>
{
    private static readonly TimeSpan BaseTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PerItemTimeout = TimeSpan.FromMinutes(1);
    private const int MaxPushes = 2;

    public async Task<Result<Unit>> Handle(SweepStaleScheduleRunsCommand request, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var llmSettings = await llmSettingsRepository.GetAsync(cancellationToken)
            ?? DomainLlmSettings.CreateDefault(now);
        var runs = await runRepository.GetRunningRunsWithItemsAsync(cancellationToken);
        bool changed = false;

        foreach (var run in runs)
        {
            // Timeout batch boyutuyla ölçeklenir: sabit değer ALL-fleet koşusunda
            // callback'lerle yarışıp sahte Failed üretir.
            var timeout = BaseTimeout + PerItemTimeout * run.Items.Count;
            var staleItems = run.Items
                .Where(i => i.Status == ScheduleRunItemStatus.AwaitingLlm && now - i.LastPushedAtUtc > timeout)
                .ToList();
            if (staleItems.Count == 0)
            {
                continue;
            }

            var toRepush = staleItems.Where(i => i.PushCount < MaxPushes).ToList();
            var toFail = staleItems.Where(i => i.PushCount >= MaxPushes).ToList();

            foreach (var item in toFail)
            {
                item.MarkFailed("No agent response after re-push timeout.", now);
                changed = true;
            }

            if (toRepush.Count > 0)
            {
                var targets = await targetRepository.GetTargetsAsync(
                    toRepush.Select(i => i.AiUserId).ToList(), cancellationToken);
                var targetByUser = targets.ToDictionary(t => t.UserId);

                var items = toRepush
                    .Where(i => targetByUser.ContainsKey(i.AiUserId))
                    .Select(i =>
                    {
                        var t = targetByUser[i.AiUserId];
                        return new ScheduleRunRequestItem(
                            i.Id, i.AiUserId, t.Username, t.PersonaSummary, t.PostsLast3Days, t.LastPostAtUtc);
                    })
                    .ToList();

                if (items.Count > 0)
                {
                    var windowEnd = TurkeySchedulingWindow.DayEndUtc(run.RunDate);
                    var message = new ScheduleRunRequest(
                        run.Id, run.ThresholdSnapshot, run.MaxPostsSnapshot, run.PostLengthGuideSnapshot,
                        (int)run.StrategySnapshot, now.AddMinutes(5), windowEnd, items,
                        Model: llmSettings.Model, ScoringModel: llmSettings.ScoringModel,
                        ThinkingEnabled: llmSettings.ThinkingEnabled,
                        ScoringTokensPerAccount: llmSettings.ScoringTokensPerAccount,
                        CompositionTokensPerPost: llmSettings.CompositionTokensPerPost);
                    await hubSender.SendAsync(message, cancellationToken);
                }

                foreach (var item in toRepush)
                {
                    item.RecordRepush(now);
                    changed = true;
                }
            }

            run.TryComplete(now);
        }

        if (changed)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result<Unit>.Success(Unit.Value);
    }
}
