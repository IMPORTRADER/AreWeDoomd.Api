using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Application.Features.AiManagement.Queries.GetAgentOpsLogs;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Application.AiManagement;

public sealed class GetAgentOpsLogsQueryHandlerTests
{
    private readonly Mock<IAgentOpsLogReader> _reader = new();

    private GetAgentOpsLogsQueryHandler CreateHandler() => new(_reader.Object);

    private static AgentOpsLogRecord MakeRecord() =>
        new(DateTimeOffset.UtcNow, "info", "admin", "hello");

    private static AgentOpsLogPage MakePage(IReadOnlyList<AgentOpsLogRecord> items, string? nextCursor, bool hasMore, bool logAvailable = true) =>
        new(items, nextCursor, hasMore, logAvailable);

    [Fact]
    public async Task Handle_MapsFilterAndClampsPageSize()
    {
        var aiUserId = Guid.NewGuid();
        var page = MakePage([], null, false);

        _reader.Setup(r => r.ReadAsync(
            It.Is<AgentOpsLogFilter>(f =>
                f.Level == "error" &&
                f.Source == "llm_provider" &&
                f.AiUserId == aiUserId.ToString()),
            null,
            100,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        var query = new GetAgentOpsLogsQuery("error", "llm_provider", aiUserId, null, null, null, 500);
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _reader.Verify(r => r.ReadAsync(
            It.Is<AgentOpsLogFilter>(f =>
                f.Level == "error" &&
                f.Source == "llm_provider" &&
                f.AiUserId == aiUserId.ToString()),
            null,
            100,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PassesThroughPage()
    {
        var record = MakeRecord();
        var page = MakePage([record], "2026-07-06:1", true);

        _reader.Setup(r => r.ReadAsync(
            It.IsAny<AgentOpsLogFilter>(),
            null,
            20,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        var query = new GetAgentOpsLogsQuery(null, null, null, null, null, null, 20);
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.Items.Count.ShouldBe(1);
        result.Value.NextCursor.ShouldBe("2026-07-06:1");
        result.Value.HasMore.ShouldBeTrue();
        result.Value.LogAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task Handle_MapsAiUserIdGuidToString()
    {
        var aiUserId = Guid.NewGuid();
        var page = MakePage([], null, false);

        _reader.Setup(r => r.ReadAsync(
            It.Is<AgentOpsLogFilter>(f => f.AiUserId == aiUserId.ToString()),
            null,
            10,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        var query = new GetAgentOpsLogsQuery(null, null, aiUserId, null, null, null, 10);
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _reader.Verify(r => r.ReadAsync(
            It.Is<AgentOpsLogFilter>(f => f.AiUserId == aiUserId.ToString()),
            null,
            10,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ClampsPageSizeAboveMax()
    {
        var page = MakePage([], null, false);

        _reader.Setup(r => r.ReadAsync(
            It.IsAny<AgentOpsLogFilter>(),
            null,
            100,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        var query = new GetAgentOpsLogsQuery(null, null, null, null, null, null, 999);
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _reader.Verify(r => r.ReadAsync(
            It.IsAny<AgentOpsLogFilter>(),
            null,
            100,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenLogUnavailable_ReturnsSuccessWithLogAvailableFalse()
    {
        var page = MakePage([], null, false, logAvailable: false);

        _reader.Setup(r => r.ReadAsync(
            It.IsAny<AgentOpsLogFilter>(),
            null,
            10,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        var query = new GetAgentOpsLogsQuery(null, null, null, null, null, null, 10);
        var result = await CreateHandler().Handle(query, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.LogAvailable.ShouldBeFalse();
        result.Value.Items.ShouldBeEmpty();
    }
}
