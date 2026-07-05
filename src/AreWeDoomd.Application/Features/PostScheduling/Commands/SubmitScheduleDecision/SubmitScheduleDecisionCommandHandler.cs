using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Results;
using AreWeDoomd.Domain.Scheduling;
using MediatR;

namespace AreWeDoomd.Application.Features.PostScheduling.Commands.SubmitScheduleDecision;

public sealed class SubmitScheduleDecisionCommandHandler(
    IScheduleRunRepository runRepository,
    IScheduledPostRepository scheduledPostRepository,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork,
    ISchedulePublisherWaker publisherWaker)
    : IRequestHandler<SubmitScheduleDecisionCommand, Result<Unit>>
{
    private static readonly TimeSpan ClampLead = TimeSpan.FromMinutes(5);

    public async Task<Result<Unit>> Handle(SubmitScheduleDecisionCommand request, CancellationToken cancellationToken)
    {
        var run = await runRepository.GetRunOfItemAsync(request.RunItemId, cancellationToken);
        var item = run?.Items.FirstOrDefault(i => i.Id == request.RunItemId);
        if (run is null || item is null)
        {
            return Result<Unit>.NotFound("scheduling.item_not_found", "Schedule run item not found.");
        }

        // Sahiplik: secret'ı tutan herkes rastgele item'lara karar yazamasın.
        if (item.AiUserId != request.CallerAiUserId)
        {
            return Result<Unit>.Forbidden("scheduling.not_owner", "Caller does not own this run item.");
        }

        // İdempotens: yalnız AwaitingLlm'den kabul — sweep↔geç callback yarışı ve
        // HTTP retry çift-yazımı burada tek guard ile çözülür.
        if (item.Status != ScheduleRunItemStatus.AwaitingLlm)
        {
            return Result<Unit>.Conflict("scheduling.already_decided", "Decision already recorded for this item.");
        }

        var now = dateTimeProvider.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.ErrorDetail))
        {
            item.MarkFailed(request.ErrorDetail, now);
            run.TryComplete(now);
            var errorSaved = await unitOfWork.TrySaveChangesAsync(cancellationToken);
            if (!errorSaved)
            {
                return Result<Unit>.Conflict("scheduling.already_decided", "Decision already recorded for this item.");
            }
            return Result<Unit>.Success(Unit.Value);
        }

        int score = Math.Clamp(request.DesireScore, 0, 100);
        bool passed = score >= run.ThresholdSnapshot; // otoriter karşılaştırma: her zaman snapshot ile

        int surviving = 0;
        int dropped = 0;

        if (passed)
        {
            var windowEnd = TurkeySchedulingWindow.DayEndUtc(run.RunDate);
            var accepted = request.Posts.Take(run.MaxPostsSnapshot).ToList();
            dropped = request.Posts.Count - accepted.Count;

            foreach (var submitted in accepted)
            {
                var time = submitted.ScheduledAtUtc;
                bool adjusted = false;

                if (time < now)
                {
                    time = now + ClampLead; // geçmişte ama gün içinde → clamp
                    adjusted = true;
                }

                if (time >= windowEnd)
                {
                    // Gün sonu ötesi (clamp sonucu taşanlar dahil) → yalnız bu gönderi düşer.
                    dropped++;
                    continue;
                }

                var scheduledPost = ScheduledPost.Create(
                    item.Id, item.AiUserId, submitted.Content, time, adjusted, now);
                await scheduledPostRepository.AddAsync(scheduledPost, cancellationToken);
                surviving++;
            }
        }

        item.RecordDecision(
            score, request.Reasoning, request.RequestedPostCount, dropped,
            request.ModelUsed, passed, surviving, now);
        run.TryComplete(now);

        var decisionSaved = await unitOfWork.TrySaveChangesAsync(cancellationToken);
        if (!decisionSaved)
        {
            return Result<Unit>.Conflict("scheduling.already_decided", "Decision already recorded for this item.");
        }

        if (surviving > 0)
        {
            publisherWaker.Wake(); // adaptif poller'ı erken uyandır
        }

        return Result<Unit>.Success(Unit.Value);
    }
}
