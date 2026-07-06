using AreWeDoomd.Domain.Scheduling;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Domain.Scheduling;

public sealed class ScheduleRunItemTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 5, 10, 0, 0, TimeSpan.Zero);

    private static ScheduleRunItem NewItem() =>
        ScheduleRunItem.Create(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 7, 5), Now);

    [Fact]
    public void Create_ShouldStartAwaitingLlm_WithPushCountOne()
    {
        var item = NewItem();
        item.Status.ShouldBe(ScheduleRunItemStatus.AwaitingLlm);
        item.PushCount.ShouldBe(1);
    }

    [Fact]
    public void RecordDecision_WhenPassedThreshold_ShouldComplete()
    {
        var item = NewItem();
        item.RecordDecision(72, "canı istedi", requestedPostCount: 2, droppedPostCount: 0,
            modelUsed: "m", passedThreshold: true, survivingPostCount: 2, Now).ShouldBeTrue();
        item.Status.ShouldBe(ScheduleRunItemStatus.Completed);
        item.DesireScore.ShouldBe(72);
    }

    [Fact]
    public void RecordDecision_WhenBelowThreshold_ShouldSetBelowThreshold()
    {
        var item = NewItem();
        item.RecordDecision(30, "sakin gün", 0, 0, "m", passedThreshold: false, survivingPostCount: 0, Now);
        item.Status.ShouldBe(ScheduleRunItemStatus.BelowThreshold);
    }

    [Fact]
    public void RecordDecision_WhenPassedButNoSurvivingPosts_ShouldFail()
    {
        var item = NewItem();
        item.RecordDecision(80, "r", 2, 2, "m", passedThreshold: true, survivingPostCount: 0, Now);
        item.Status.ShouldBe(ScheduleRunItemStatus.Failed);
    }

    [Fact]
    public void RecordDecision_WhenAlreadyTerminal_ShouldReturnFalse()
    {
        var item = NewItem();
        item.RecordDecision(72, "r", 1, 0, "m", true, 1, Now);
        item.RecordDecision(50, "geç callback", 1, 0, "m", true, 1, Now).ShouldBeFalse();
        item.DesireScore.ShouldBe(72);
    }

    [Fact]
    public void RecordRepush_ShouldIncrementPushCount()
    {
        var item = NewItem();
        item.RecordRepush(Now.AddMinutes(10));
        item.PushCount.ShouldBe(2);
    }
}
