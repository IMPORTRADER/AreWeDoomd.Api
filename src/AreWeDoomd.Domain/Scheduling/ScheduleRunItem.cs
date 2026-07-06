namespace AreWeDoomd.Domain.Scheduling;

public sealed class ScheduleRunItem
{
    public Guid Id { get; private set; }
    public Guid ScheduleRunId { get; private set; }
    public Guid AiUserId { get; private set; }
    public DateOnly RunDate { get; private set; } // Denormalize: (AiUserId, RunDate) filtered unique index için
    public ScheduleRunItemStatus Status { get; private set; }
    public int? DesireScore { get; private set; }
    public string? Reasoning { get; private set; }
    public int? RequestedPostCount { get; private set; }
    public int DroppedPostCount { get; private set; }
    public string? ModelUsed { get; private set; }
    public string? ErrorDetail { get; private set; }
    public int PushCount { get; private set; }
    public DateTimeOffset LastPushedAtUtc { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = null!; // Optimistic concurrency token

    private ScheduleRunItem() { } // EF Core

    private ScheduleRunItem(Guid id, Guid scheduleRunId, Guid aiUserId, DateOnly runDate, DateTimeOffset now)
    {
        if (aiUserId == Guid.Empty)
        {
            throw new ArgumentException("AiUserId cannot be empty.", nameof(aiUserId));
        }

        Id = id;
        ScheduleRunId = scheduleRunId;
        AiUserId = aiUserId;
        RunDate = runDate;
        Status = ScheduleRunItemStatus.AwaitingLlm;
        PushCount = 1;
        LastPushedAtUtc = now;
        CreatedAt = now;
    }

    public static ScheduleRunItem Create(Guid scheduleRunId, Guid aiUserId, DateOnly runDate, DateTimeOffset now)
        => new(Guid.NewGuid(), scheduleRunId, aiUserId, runDate, now);

    /// <summary>
    /// LLM kararını kaydeder. Idempotens: yalnız AwaitingLlm durumunda kabul eder,
    /// aksi halde false döner (geç callback / sweep yarışı çift yazımı burada ölür).
    /// </summary>
    public bool RecordDecision(
        int desireScore, string? reasoning, int requestedPostCount, int droppedPostCount,
        string? modelUsed, bool passedThreshold, int survivingPostCount, DateTimeOffset now)
    {
        if (Status != ScheduleRunItemStatus.AwaitingLlm)
        {
            return false;
        }

        DesireScore = Math.Clamp(desireScore, 0, 100);
        Reasoning = Truncate(reasoning, 1000);
        RequestedPostCount = requestedPostCount;
        DroppedPostCount = droppedPostCount;
        ModelUsed = Truncate(modelUsed, 200);
        CompletedAt = now;

        if (!passedThreshold)
        {
            Status = ScheduleRunItemStatus.BelowThreshold;
        }
        else if (survivingPostCount > 0)
        {
            Status = ScheduleRunItemStatus.Completed;
        }
        else
        {
            Status = ScheduleRunItemStatus.Failed;
            ErrorDetail = "All proposed posts were dropped by server-side validation.";
        }

        return true;
    }

    public bool MarkFailed(string errorDetail, DateTimeOffset now)
    {
        if (Status != ScheduleRunItemStatus.AwaitingLlm)
        {
            return false;
        }

        Status = ScheduleRunItemStatus.Failed;
        ErrorDetail = Truncate(errorDetail, 1000);
        CompletedAt = now;
        return true;
    }

    public bool MarkSuperseded(DateTimeOffset now)
    {
        if (Status == ScheduleRunItemStatus.Superseded)
        {
            return false;
        }

        Status = ScheduleRunItemStatus.Superseded;
        CompletedAt = now;
        return true;
    }

    public void RecordRepush(DateTimeOffset now)
    {
        PushCount++;
        LastPushedAtUtc = now;
    }

    private static string? Truncate(string? value, int max)
    {
        if (value is null)
        {
            return null;
        }
        return value.Length <= max ? value : value[..max];
    }
}
