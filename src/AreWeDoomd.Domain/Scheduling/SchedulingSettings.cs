namespace AreWeDoomd.Domain.Scheduling;

public sealed class SchedulingSettings
{
    public static readonly Guid SingletonId = new("7b5a1a6e-0000-4000-8000-000000000001");

    public Guid Id { get; private set; }
    public int DesireThreshold { get; private set; }
    public int MaxPostsPerDay { get; private set; }
    public int PostLengthGuide { get; private set; } // Prompt'a yazılan hedef karakter uzunluğu rehberi
    public SchedulingLatePolicy LatePolicy { get; private set; }
    public int LateGraceHours { get; private set; }
    public LlmSchedulingStrategy Strategy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private SchedulingSettings() { } // EF Core

    public static SchedulingSettings CreateDefault(DateTimeOffset now)
    {
        var settings = new SchedulingSettings { Id = SingletonId };
        settings.Update(
            desireThreshold: 60, maxPostsPerDay: 3, postLengthGuide: 500,
            latePolicy: SchedulingLatePolicy.Expire, lateGraceHours: 3,
            strategy: LlmSchedulingStrategy.TwoStage, now: now);
        return settings;
    }

    public void Update(
        int desireThreshold, int maxPostsPerDay, int postLengthGuide,
        SchedulingLatePolicy latePolicy, int lateGraceHours,
        LlmSchedulingStrategy strategy, DateTimeOffset now)
    {
        if (desireThreshold is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(desireThreshold));
        }
        if (maxPostsPerDay is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPostsPerDay));
        }
        if (postLengthGuide is < 50 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(postLengthGuide));
        }
        if (lateGraceHours is < 0 or > 24)
        {
            throw new ArgumentOutOfRangeException(nameof(lateGraceHours));
        }

        DesireThreshold = desireThreshold;
        MaxPostsPerDay = maxPostsPerDay;
        PostLengthGuide = postLengthGuide;
        LatePolicy = latePolicy;
        LateGraceHours = lateGraceHours;
        Strategy = strategy;
        UpdatedAt = now;
    }
}
