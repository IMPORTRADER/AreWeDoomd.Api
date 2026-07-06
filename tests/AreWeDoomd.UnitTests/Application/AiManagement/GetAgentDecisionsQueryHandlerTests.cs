using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Application.Features.AiManagement.Queries.GetAgentDecisions;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Application.AiManagement;

public sealed class GetAgentDecisionsQueryHandlerTests
{
    private readonly Mock<IDecisionLogReader> _reader = new();

    private GetAgentDecisionsQueryHandler CreateHandler() => new(_reader.Object);

    private static DecisionLogRecord MakeRecord(string aiUserId) =>
        new(DateTimeOffset.UtcNow, aiUserId, "act-1", "NewPost", "executed",
            "CreatePost", null, null, null, null, null, null, null, null, null, null);

    private static DecisionLogPage MakePage(IReadOnlyList<DecisionLogRecord> items, string? nextCursor, bool hasMore) =>
        new(items, nextCursor, hasMore, true);

    [Fact]
    public async Task Handle_HappyPath_ReturnsPagedResultSuccess()
    {
        var record = MakeRecord("user-1");
        var page = MakePage([record], "cursor-abc", true);

        _reader.Setup(r => r.ReadAsync(
            It.Is<DecisionLogFilter>(f =>
                f.AiUserId == null &&
                f.Action == null &&
                f.Outcome == null &&
                f.FromUtc == null &&
                f.ToUtc == null),
            null,
            10,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        var query = new GetAgentDecisionsQuery(null, null, null, null, null, null, 10);
        var result = await CreateHandler().Handle(query, default);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Items.Count.ShouldBe(1);
        result.Value.NextCursor.ShouldBe("cursor-abc");
        result.Value.HasMore.ShouldBeTrue();
        result.Value.LogAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_MapsAiUserIdGuidToString()
    {
        var aiUserId = Guid.NewGuid();
        var page = MakePage([], null, false);

        _reader.Setup(r => r.ReadAsync(
            It.Is<DecisionLogFilter>(f => f.AiUserId == aiUserId.ToString()),
            null,
            10,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        var query = new GetAgentDecisionsQuery(aiUserId, null, null, null, null, null, 10);
        var result = await CreateHandler().Handle(query, default);

        result.IsSuccess.ShouldBeTrue();
        _reader.Verify(r => r.ReadAsync(
            It.Is<DecisionLogFilter>(f => f.AiUserId == aiUserId.ToString()),
            null,
            10,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PassesCursorThrough()
    {
        var cursor = "my-cursor-token";
        var page = MakePage([], null, false);

        _reader.Setup(r => r.ReadAsync(
            It.IsAny<DecisionLogFilter>(),
            cursor,
            10,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        var query = new GetAgentDecisionsQuery(null, null, null, null, null, cursor, 10);
        var result = await CreateHandler().Handle(query, default);

        result.IsSuccess.ShouldBeTrue();
        _reader.Verify(r => r.ReadAsync(
            It.IsAny<DecisionLogFilter>(),
            cursor,
            10,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenLogUnavailable_ReturnsSuccessWithLogAvailableFalse()
    {
        var unavailablePage = new DecisionLogPage([], null, false, false);

        _reader.Setup(r => r.ReadAsync(
            It.IsAny<DecisionLogFilter>(),
            null,
            10,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(unavailablePage);

        var query = new GetAgentDecisionsQuery(null, null, null, null, null, null, 10);
        var result = await CreateHandler().Handle(query, default);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.LogAvailable.ShouldBeFalse();
        result.Value.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_ClampsPageSizeAboveMax()
    {
        var page = MakePage([], null, false);

        _reader.Setup(r => r.ReadAsync(
            It.IsAny<DecisionLogFilter>(),
            null,
            100,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        var query = new GetAgentDecisionsQuery(null, null, null, null, null, null, 999);
        var result = await CreateHandler().Handle(query, default);

        result.IsSuccess.ShouldBeTrue();
        _reader.Verify(r => r.ReadAsync(
            It.IsAny<DecisionLogFilter>(),
            null,
            100,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ClampsPageSizeBelowMin()
    {
        var page = MakePage([], null, false);

        _reader.Setup(r => r.ReadAsync(
            It.IsAny<DecisionLogFilter>(),
            null,
            1,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        var query = new GetAgentDecisionsQuery(null, null, null, null, null, null, 0);
        var result = await CreateHandler().Handle(query, default);

        result.IsSuccess.ShouldBeTrue();
        _reader.Verify(r => r.ReadAsync(
            It.IsAny<DecisionLogFilter>(),
            null,
            1,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MapsAllFilterFields()
    {
        var aiUserId = Guid.NewGuid();
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 6, 30);
        var page = MakePage([], null, false);

        _reader.Setup(r => r.ReadAsync(
            It.Is<DecisionLogFilter>(f =>
                f.AiUserId == aiUserId.ToString() &&
                f.Action == "CreatePost" &&
                f.Outcome == "executed" &&
                f.FromUtc == from &&
                f.ToUtc == to),
            null,
            10,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        var query = new GetAgentDecisionsQuery(aiUserId, "CreatePost", "executed", from, to, null, 10);
        var result = await CreateHandler().Handle(query, default);

        result.IsSuccess.ShouldBeTrue();
    }
}
