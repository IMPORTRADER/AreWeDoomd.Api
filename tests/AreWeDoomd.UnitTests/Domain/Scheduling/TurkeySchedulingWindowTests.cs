using AreWeDoomd.Domain.Scheduling;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Domain.Scheduling;

public sealed class TurkeySchedulingWindowTests
{
    [Fact]
    public void TurkeyDateOf_WhenUtcIsBefore21_ShouldReturnSameCalendarDay()
    {
        var utc = new DateTimeOffset(2026, 7, 5, 20, 59, 0, TimeSpan.Zero); // 23:59 TRT
        TurkeySchedulingWindow.TurkeyDateOf(utc).ShouldBe(new DateOnly(2026, 7, 5));
    }

    [Fact]
    public void TurkeyDateOf_WhenUtcIsAfter21_ShouldReturnNextCalendarDay()
    {
        var utc = new DateTimeOffset(2026, 7, 5, 21, 30, 0, TimeSpan.Zero); // 00:30 TRT ertesi gün
        TurkeySchedulingWindow.TurkeyDateOf(utc).ShouldBe(new DateOnly(2026, 7, 6));
    }

    [Fact]
    public void DayStartUtc_ShouldBePreviousDay2100Utc()
    {
        TurkeySchedulingWindow.DayStartUtc(new DateOnly(2026, 7, 5))
            .ShouldBe(new DateTimeOffset(2026, 7, 4, 21, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void DayEndUtc_ShouldBeSameDay2100Utc()
    {
        TurkeySchedulingWindow.DayEndUtc(new DateOnly(2026, 7, 5))
            .ShouldBe(new DateTimeOffset(2026, 7, 5, 21, 0, 0, TimeSpan.Zero));
    }
}
