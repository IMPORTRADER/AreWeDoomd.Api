using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Infrastructure.Common.Options;
using AreWeDoomd.Infrastructure.Common.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Infrastructure;

public sealed class FileDecisionLogReaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "awd-reader-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private FileDecisionLogReader CreateReader()
    {
        var options = Options.Create(new DecisionLogOptions { RootPath = _dir });
        return new FileDecisionLogReader(options, NullLogger<FileDecisionLogReader>.Instance);
    }

    private static string Line(string ts, string aiUserId, string activityId, string activityType, string outcome, string? action = null)
    {
        var actionPart = action != null ? $",\"action\":\"{action}\"" : string.Empty;
        return $"{{\"ts\":\"{ts}\",\"aiUserId\":\"{aiUserId}\",\"activityId\":\"{activityId}\",\"activityType\":\"{activityType}\",\"outcome\":\"{outcome}\"{actionPart}}}";
    }

    private void WriteFile(string date, IEnumerable<string> lines)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllLines(Path.Combine(_dir, $"decisions-{date}.jsonl"), lines);
    }

    [Fact]
    public async Task ReadAsync_WhenDirectoryMissing_ShouldReturnUnavailablePage()
    {
        var reader = CreateReader();

        var page = await reader.ReadAsync(new DecisionLogFilter(), cursor: null, pageSize: 10, ct: CancellationToken.None);

        page.LogAvailable.ShouldBeFalse();
        page.Items.ShouldBeEmpty();
        page.NextCursor.ShouldBeNull();
        page.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task ReadAsync_ShouldReturnNewestFirstAcrossDays()
    {
        WriteFile("2026-07-02", [
            Line("2026-07-02T08:00:00+00:00", "a1", "act-1", "CommentCreated", "executed"),
            Line("2026-07-02T09:00:00+00:00", "a1", "act-2", "CommentCreated", "executed"),
        ]);
        WriteFile("2026-07-03", [
            Line("2026-07-03T08:00:00+00:00", "a1", "act-3", "CommentCreated", "executed"),
            Line("2026-07-03T09:00:00+00:00", "a1", "act-4", "CommentCreated", "executed"),
        ]);

        var reader = CreateReader();
        var page = await reader.ReadAsync(new DecisionLogFilter(), cursor: null, pageSize: 3, ct: CancellationToken.None);

        page.LogAvailable.ShouldBeTrue();
        page.Items.Count.ShouldBe(3);
        // Newest day (2026-07-03) reversed: act-4 first, then act-3
        page.Items[0].ActivityId.ShouldBe("act-4");
        page.Items[1].ActivityId.ShouldBe("act-3");
        // Then one from older day (2026-07-02) reversed: act-2 first
        page.Items[2].ActivityId.ShouldBe("act-2");
        // NextCursor points into old day at position 1
        page.NextCursor.ShouldBe("2026-07-02:1");
        page.HasMore.ShouldBeTrue();
    }

    [Fact]
    public async Task ReadAsync_WithCursor_ShouldContinueWithoutOverlap()
    {
        WriteFile("2026-07-02", [
            Line("2026-07-02T08:00:00+00:00", "a1", "act-1", "CommentCreated", "executed"),
            Line("2026-07-02T09:00:00+00:00", "a1", "act-2", "CommentCreated", "executed"),
        ]);
        WriteFile("2026-07-03", [
            Line("2026-07-03T08:00:00+00:00", "a1", "act-3", "CommentCreated", "executed"),
            Line("2026-07-03T09:00:00+00:00", "a1", "act-4", "CommentCreated", "executed"),
        ]);

        var reader = CreateReader();

        // Page 1
        var page1 = await reader.ReadAsync(new DecisionLogFilter(), cursor: null, pageSize: 2, ct: CancellationToken.None);
        page1.Items.Count.ShouldBe(2);
        page1.NextCursor.ShouldNotBeNull();
        page1.HasMore.ShouldBeTrue();

        var page1Ids = page1.Items.Select(r => r.ActivityId).ToHashSet();

        // Page 2
        var page2 = await reader.ReadAsync(new DecisionLogFilter(), cursor: page1.NextCursor, pageSize: 2, ct: CancellationToken.None);
        page2.Items.Count.ShouldBe(2);
        page2.NextCursor.ShouldBeNull();
        page2.HasMore.ShouldBeFalse();

        // No overlap
        foreach (var item in page2.Items)
        {
            page1Ids.ShouldNotContain(item.ActivityId);
        }

        // Correct order: newest day first across both pages combined
        var all = page1.Items.Concat(page2.Items).ToList();
        all[0].ActivityId.ShouldBe("act-4");
        all[1].ActivityId.ShouldBe("act-3");
        all[2].ActivityId.ShouldBe("act-2");
        all[3].ActivityId.ShouldBe("act-1");
    }

    [Fact]
    public async Task ReadAsync_ShouldApplyFilters()
    {
        WriteFile("2026-07-03", [
            Line("2026-07-03T08:00:00+00:00", "a1", "act-1", "CommentCreated", "executed"),
            Line("2026-07-03T09:00:00+00:00", "a2", "act-2", "CommentCreated", "executed"),
            Line("2026-07-03T10:00:00+00:00", "a1", "act-3", "CommentCreated", "dropped"),
            Line("2026-07-03T11:00:00+00:00", "a1", "act-4", "CommentCreated", "executed"),
        ]);

        var reader = CreateReader();
        var filter = new DecisionLogFilter(AiUserId: "a1", Outcome: "executed");
        var page = await reader.ReadAsync(filter, cursor: null, pageSize: 10, ct: CancellationToken.None);

        page.Items.Count.ShouldBe(2);
        page.Items.ShouldAllBe(r => r.AiUserId == "a1" && r.Outcome == "executed");
        // Newest first
        page.Items[0].ActivityId.ShouldBe("act-4");
        page.Items[1].ActivityId.ShouldBe("act-1");
    }

    [Fact]
    public async Task ReadAsync_ShouldSkipTornAndInvalidLines()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllLines(Path.Combine(_dir, "decisions-2026-07-03.jsonl"), [
            Line("2026-07-03T10:00:00+00:00", "a1", "act-1", "CommentCreated", "executed"),
            "{\"ts\":\"2026-07-03T10:01:00+00:00\",\"aiUserId\":\"a1\",\"activi",  // torn line
            string.Empty,                                                              // blank line
        ]);

        var reader = CreateReader();
        var page = await reader.ReadAsync(new DecisionLogFilter(), cursor: null, pageSize: 10, ct: CancellationToken.None);

        page.Items.Count.ShouldBe(1);
        page.Items[0].ActivityId.ShouldBe("act-1");
    }

    [Fact]
    public async Task GetDailyStatsAsync_ShouldCountOutcomes()
    {
        var now = new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);
        // Two executed: act-1 (2h ago, outside last hour), act-2 (30 min ago, inside last hour)
        // One dropped: act-3
        // One llm_failed: act-4
        WriteFile("2026-07-03", [
            Line("2026-07-03T10:00:00+00:00", "a1", "act-1", "CommentCreated", "executed"),
            Line("2026-07-03T11:30:00+00:00", "a1", "act-2", "CommentCreated", "executed"),
            Line("2026-07-03T11:00:00+00:00", "a1", "act-3", "CommentCreated", "dropped"),
            Line("2026-07-03T11:15:00+00:00", "a1", "act-4", "CommentCreated", "llm_failed"),
        ]);

        var reader = CreateReader();
        var stats = await reader.GetDailyStatsAsync(new DateOnly(2026, 7, 3), now, CancellationToken.None);

        stats.ShouldNotBeNull();
        stats!.Total.ShouldBe(4);
        stats.Executed.ShouldBe(2);
        stats.Dropped.ShouldBe(1);
        stats.Failed.ShouldBe(1);
        stats.ActionsLastHour.ShouldBe(1);
    }

    [Fact]
    public async Task GetDailyStatsAsync_WhenFileMissing_ShouldReturnNull()
    {
        Directory.CreateDirectory(_dir);
        var reader = CreateReader();

        var stats = await reader.GetDailyStatsAsync(new DateOnly(2026, 7, 3), DateTimeOffset.UtcNow, CancellationToken.None);

        stats.ShouldBeNull();
    }
}
