using AreWeDoomd.AgentService.Logging;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.AgentService.Logging;

public sealed class DecisionLogFileAppenderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "awd-declog-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) { Directory.Delete(_dir, recursive: true); }
    }

    private static DecisionLogEntry Entry(DateTimeOffset ts) =>
        new(ts, "ai-1", "act-1", "CommentCreated", DecisionOutcome.Executed);

    [Fact]
    public void Append_ShouldCreateDailyFileAndWriteOneLine()
    {
        var appender = new DecisionLogFileAppender(_dir);
        var ts = DateTimeOffset.Parse("2026-07-04T23:59:00Z");

        appender.Append(Entry(ts));
        appender.Append(Entry(ts));

        string file = Path.Combine(_dir, "decisions-2026-07-04.jsonl");
        File.Exists(file).ShouldBeTrue();
        File.ReadAllLines(file).Length.ShouldBe(2);
    }

    [Fact]
    public void Append_ShouldRotateByUtcDate()
    {
        var appender = new DecisionLogFileAppender(_dir);

        appender.Append(Entry(DateTimeOffset.Parse("2026-07-04T23:59:00Z")));
        appender.Append(Entry(DateTimeOffset.Parse("2026-07-05T00:01:00Z")));

        File.Exists(Path.Combine(_dir, "decisions-2026-07-04.jsonl")).ShouldBeTrue();
        File.Exists(Path.Combine(_dir, "decisions-2026-07-05.jsonl")).ShouldBeTrue();
    }

    [Fact]
    public void CleanupOldFiles_ShouldDeleteOnlyExpiredFiles()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "decisions-2020-01-01.jsonl"), "old\n");
        string todayFile = $"decisions-{DateTime.UtcNow:yyyy-MM-dd}.jsonl";
        File.WriteAllText(Path.Combine(_dir, todayFile), "new\n");
        File.WriteAllText(Path.Combine(_dir, "not-a-decision.txt"), "keep\n");
        var appender = new DecisionLogFileAppender(_dir);

        int deleted = appender.CleanupOldFiles(retentionDays: 30);

        deleted.ShouldBe(1);
        File.Exists(Path.Combine(_dir, "decisions-2020-01-01.jsonl")).ShouldBeFalse();
        File.Exists(Path.Combine(_dir, todayFile)).ShouldBeTrue();
        File.Exists(Path.Combine(_dir, "not-a-decision.txt")).ShouldBeTrue();
    }
}
