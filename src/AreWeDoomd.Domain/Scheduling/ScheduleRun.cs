namespace AreWeDoomd.Domain.Scheduling;

public sealed class ScheduleRun
{
    public Guid Id { get; private set; }
    public DateOnly RunDate { get; private set; }
    public Guid TriggeredByUserId { get; private set; }
    public int ThresholdSnapshot { get; private set; }
    public int MaxPostsSnapshot { get; private set; }
    public int PostLengthGuideSnapshot { get; private set; }
    public LlmSchedulingStrategy StrategySnapshot { get; private set; }
    public ScheduleRunStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private readonly List<ScheduleRunItem> _items = [];
    public IReadOnlyCollection<ScheduleRunItem> Items => _items;

    private ScheduleRun() { } // EF Core

    private ScheduleRun(
        Guid id, DateOnly runDate, Guid triggeredByUserId, int thresholdSnapshot,
        int maxPostsSnapshot, int postLengthGuideSnapshot, LlmSchedulingStrategy strategySnapshot,
        DateTimeOffset createdAt)
    {
        if (triggeredByUserId == Guid.Empty)
        {
            throw new ArgumentException("TriggeredByUserId cannot be empty.", nameof(triggeredByUserId));
        }
        if (thresholdSnapshot is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(thresholdSnapshot));
        }

        Id = id;
        RunDate = runDate;
        TriggeredByUserId = triggeredByUserId;
        ThresholdSnapshot = thresholdSnapshot;
        MaxPostsSnapshot = maxPostsSnapshot;
        PostLengthGuideSnapshot = postLengthGuideSnapshot;
        StrategySnapshot = strategySnapshot;
        Status = ScheduleRunStatus.Running;
        CreatedAt = createdAt;
    }

    public static ScheduleRun Create(
        DateOnly runDate, Guid triggeredByUserId, int thresholdSnapshot, int maxPostsSnapshot,
        int postLengthGuideSnapshot, LlmSchedulingStrategy strategySnapshot, DateTimeOffset now)
        => new(Guid.NewGuid(), runDate, triggeredByUserId, thresholdSnapshot,
            maxPostsSnapshot, postLengthGuideSnapshot, strategySnapshot, now);

    public ScheduleRunItem AddItem(Guid aiUserId, DateTimeOffset now)
    {
        var item = ScheduleRunItem.Create(Id, aiUserId, RunDate, now);
        _items.Add(item);
        return item;
    }

    /// <summary>Tüm item'lar terminal olduğunda çağrılır; hâlâ AwaitingLlm varsa dokunmaz.</summary>
    public bool TryComplete(DateTimeOffset now)
    {
        if (Status != ScheduleRunStatus.Running)
        {
            return false;
        }
        if (_items.Any(i => i.Status == ScheduleRunItemStatus.AwaitingLlm))
        {
            return false;
        }

        Status = _items.Any(i => i.Status == ScheduleRunItemStatus.Failed)
            ? ScheduleRunStatus.CompletedWithErrors
            : ScheduleRunStatus.Completed;
        CompletedAt = now;
        return true;
    }
}
