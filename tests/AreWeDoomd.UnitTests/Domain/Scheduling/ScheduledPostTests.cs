using AreWeDoomd.Domain.Scheduling;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Domain.Scheduling;

public sealed class ScheduledPostTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 5, 10, 0, 0, TimeSpan.Zero);

    private static ScheduledPost NewPending() =>
        ScheduledPost.Create(Guid.NewGuid(), Guid.NewGuid(), "içerik", Now.AddHours(2), wasTimeAdjusted: false, Now);

    [Fact]
    public void Create_ShouldStartPending()
    {
        NewPending().Status.ShouldBe(ScheduledPostStatus.Pending);
    }

    [Fact]
    public void Create_WhenContentTooLong_ShouldThrow()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            ScheduledPost.Create(Guid.NewGuid(), Guid.NewGuid(), new string('x', 10_001), Now.AddHours(1), false, Now));
    }

    [Fact]
    public void Cancel_WhenPending_ShouldReturnTrueAndSetCancelled()
    {
        var post = NewPending();
        post.Cancel().ShouldBeTrue();
        post.Status.ShouldBe(ScheduledPostStatus.Cancelled);
    }

    [Fact]
    public void Cancel_WhenPublished_ShouldReturnFalse()
    {
        var post = NewPending();
        post.BeginPublishing(Guid.NewGuid(), Guid.NewGuid());
        post.MarkPublished(Now.AddHours(2));
        post.Cancel().ShouldBeFalse();
        post.Status.ShouldBe(ScheduledPostStatus.Published);
    }

    [Fact]
    public void MarkPublished_WhenNotPublishing_ShouldThrow()
    {
        Should.Throw<InvalidOperationException>(() => NewPending().MarkPublished(Now));
    }

    [Fact]
    public void UpdatePending_WhenPending_ShouldReturnTrueAndUpdate()
    {
        var post = NewPending();
        var newTime = Now.AddHours(5);
        post.UpdatePending("yeni içerik", newTime, Now).ShouldBeTrue();
        post.Content.ShouldBe("yeni içerik");
        post.ScheduledAtUtc.ShouldBe(newTime);
    }

    [Fact]
    public void ResetForRetry_WhenFailed_ShouldReturnTrueAndClearClaim()
    {
        var post = NewPending();
        post.BeginPublishing(Guid.NewGuid(), Guid.NewGuid());
        post.MarkFailed("boom");
        post.ResetForRetry().ShouldBeTrue();
        post.Status.ShouldBe(ScheduledPostStatus.Pending);
        post.ClaimToken.ShouldBeNull();
    }
}
