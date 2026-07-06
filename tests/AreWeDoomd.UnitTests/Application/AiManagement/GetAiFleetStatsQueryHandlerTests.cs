using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Application.Features.AiManagement.Queries.GetAiFleetStats;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Application.AiManagement;

public sealed class GetAiFleetStatsQueryHandlerTests
{
    private static readonly DateTimeOffset FixedNow = DateTimeOffset.Parse("2026-07-04T15:30:00Z");
    private static readonly DateOnly Today = DateOnly.FromDateTime(FixedNow.UtcDateTime);

    private readonly Mock<IAiUserReadRepository> _repo = new();
    private readonly Mock<IDecisionLogReader> _reader = new();
    private readonly Mock<IDateTimeProvider> _clock = new();

    public GetAiFleetStatsQueryHandlerTests()
    {
        _clock.Setup(c => c.UtcNow).Returns(FixedNow);
    }

    private GetAiFleetStatsQueryHandler CreateHandler() =>
        new(_repo.Object, _reader.Object, _clock.Object);

    [Fact]
    public async Task Handle_HappyPath_AggregatesAllStats()
    {
        _repo.Setup(r => r.CountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Total: 42, WithPersonality: 30));

        var dailyStats = new DecisionLogDailyStats(Today, 100, 80, 10, 10, 25);
        _reader.Setup(r => r.GetDailyStatsAsync(Today, FixedNow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dailyStats);

        var result = await CreateHandler().Handle(new GetAiFleetStatsQuery(), default);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.TotalAiUsers.ShouldBe(42);
        result.Value.WithPersonality.ShouldBe(30);
        result.Value.DecisionsToday.ShouldBe(100);
        result.Value.ExecutedToday.ShouldBe(80);
        result.Value.DroppedToday.ShouldBe(10);
        result.Value.FailedToday.ShouldBe(10);
        result.Value.ActionsLastHour.ShouldBe(25);
        result.Value.LogAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_CallsGetDailyStatsWithTodayFromProvider()
    {
        _repo.Setup(r => r.CountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Total: 5, WithPersonality: 3));

        _reader.Setup(r => r.GetDailyStatsAsync(Today, FixedNow, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DecisionLogDailyStats?)null);

        await CreateHandler().Handle(new GetAiFleetStatsQuery(), default);

        _reader.Verify(r => r.GetDailyStatsAsync(Today, FixedNow, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDayStatsNull_ReturnsZeroDecisionCountsWithRealUserCounts()
    {
        _repo.Setup(r => r.CountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Total: 15, WithPersonality: 8));

        _reader.Setup(r => r.GetDailyStatsAsync(Today, FixedNow, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DecisionLogDailyStats?)null);

        var result = await CreateHandler().Handle(new GetAiFleetStatsQuery(), default);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.TotalAiUsers.ShouldBe(15);
        result.Value.WithPersonality.ShouldBe(8);
        result.Value.DecisionsToday.ShouldBe(0);
        result.Value.ExecutedToday.ShouldBe(0);
        result.Value.DroppedToday.ShouldBe(0);
        result.Value.FailedToday.ShouldBe(0);
        result.Value.ActionsLastHour.ShouldBe(0);
        result.Value.LogAvailable.ShouldBeFalse();
    }
}
