namespace AreWeDoomd.Domain.Scheduling;

public sealed class ScheduledPost
{
    public const int MaxContentLength = 10_000; // Post.Content ile aynı sınır

    public Guid Id { get; private set; }
    public Guid? ScheduleRunItemId { get; private set; }
    public Guid AiUserId { get; private set; }
    public string Content { get; private set; } = null!;
    public DateTimeOffset ScheduledAtUtc { get; private set; }
    public ScheduledPostStatus Status { get; private set; }
    public Guid? PublishedPostId { get; private set; } // Claim anında ÖNCEDEN üretilir — idempotensin anahtarı
    public Guid? ClaimToken { get; private set; }
    public int AttemptCount { get; private set; }
    public bool WasTimeAdjusted { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }

    private ScheduledPost() { } // EF Core

    private ScheduledPost(
        Guid id, Guid? scheduleRunItemId, Guid aiUserId, string content,
        DateTimeOffset scheduledAtUtc, bool wasTimeAdjusted, DateTimeOffset now)
    {
        if (aiUserId == Guid.Empty)
        {
            throw new ArgumentException("AiUserId cannot be empty.", nameof(aiUserId));
        }

        Id = id;
        ScheduleRunItemId = scheduleRunItemId;
        AiUserId = aiUserId;
        SetContent(content);
        ScheduledAtUtc = scheduledAtUtc;
        Status = ScheduledPostStatus.Pending;
        WasTimeAdjusted = wasTimeAdjusted;
        CreatedAt = now;
    }

    public static ScheduledPost Create(
        Guid? scheduleRunItemId, Guid aiUserId, string content,
        DateTimeOffset scheduledAtUtc, bool wasTimeAdjusted, DateTimeOffset now)
        => new(Guid.NewGuid(), scheduleRunItemId, aiUserId, content, scheduledAtUtc, wasTimeAdjusted, now);

    /// <summary>
    /// Yalnız in-memory geçiş için; asıl claim yarış-güvenli olarak repository'deki
    /// atomik UPDATE ile yapılır (bkz. IScheduledPostRepository.TryClaimAsync).
    /// </summary>
    public void BeginPublishing(Guid claimToken, Guid publishedPostId)
    {
        if (Status != ScheduledPostStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot begin publishing from {Status}.");
        }

        Status = ScheduledPostStatus.Publishing;
        ClaimToken = claimToken;
        PublishedPostId = publishedPostId;
        AttemptCount++;
    }

    public void MarkPublished(DateTimeOffset now)
    {
        if (Status != ScheduledPostStatus.Publishing)
        {
            throw new InvalidOperationException($"Cannot mark published from {Status}.");
        }

        Status = ScheduledPostStatus.Published;
        PublishedAtUtc = now;
        ErrorMessage = null;
    }

    public void MarkFailed(string error)
    {
        Status = ScheduledPostStatus.Failed;
        ErrorMessage = error.Length <= 1000 ? error : error[..1000];
    }

    public bool Cancel()
    {
        if (Status != ScheduledPostStatus.Pending)
        {
            return false;
        }

        Status = ScheduledPostStatus.Cancelled;
        return true;
    }

    public bool Expire()
    {
        if (Status != ScheduledPostStatus.Pending)
        {
            return false;
        }

        Status = ScheduledPostStatus.Expired;
        return true;
    }

    public bool ResetForRetry()
    {
        if (Status != ScheduledPostStatus.Failed)
        {
            return false;
        }

        Status = ScheduledPostStatus.Pending;
        ClaimToken = null;
        ErrorMessage = null;
        return true;
    }

    public bool UpdatePending(string content, DateTimeOffset scheduledAtUtc, DateTimeOffset now)
    {
        if (Status != ScheduledPostStatus.Pending)
        {
            return false;
        }

        SetContent(content);
        ScheduledAtUtc = scheduledAtUtc;
        WasTimeAdjusted = false;
        return true;
    }

    private void SetContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content is required.", nameof(content));
        }

        content = content.Trim();

        if (content.Length > MaxContentLength)
        {
            throw new ArgumentOutOfRangeException(nameof(content), "Content is too long.");
        }

        Content = content;
    }
}
