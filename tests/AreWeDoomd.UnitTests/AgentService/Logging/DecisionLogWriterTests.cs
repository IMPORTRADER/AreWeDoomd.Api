using AreWeDoomd.AgentService.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.AgentService.Logging;

public sealed class DecisionLogWriterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "awd-declogw-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) { Directory.Delete(_dir, recursive: true); }
    }

    [Fact]
    public async Task TryLog_ShouldEventuallyAppendLineToDailyFile()
    {
        var options = Options.Create(new DecisionLogOptions { RootPath = _dir });
        using var writer = new DecisionLogWriter(options, NullLogger<DecisionLogWriter>.Instance);
        await writer.StartAsync(CancellationToken.None);

        bool accepted = writer.TryLog(new DecisionLogEntry(
            DateTimeOffset.UtcNow, "ai-1", "act-1", "CommentCreated", DecisionOutcome.Executed));

        accepted.ShouldBeTrue();
        string file = Path.Combine(_dir, $"decisions-{DateTime.UtcNow:yyyy-MM-dd}.jsonl");
        await WaitForFileLineAsync(file);
        await writer.StopAsync(CancellationToken.None);
        File.ReadAllLines(file).Length.ShouldBe(1);
    }

    [Fact]
    public async Task StopAsync_ShouldDrainQueuedEntriesBeforeExit()
    {
        var options = Options.Create(new DecisionLogOptions { RootPath = _dir });
        using var writer = new DecisionLogWriter(options, NullLogger<DecisionLogWriter>.Instance);
        await writer.StartAsync(CancellationToken.None);

        for (int i = 0; i < 50; i++)
        {
            writer.TryLog(new DecisionLogEntry(
                DateTimeOffset.UtcNow, "ai-1", $"act-{i}", "CommentCreated", DecisionOutcome.Executed))
                .ShouldBeTrue();
        }

        await writer.StopAsync(CancellationToken.None);

        writer.ExecuteTask.ShouldNotBeNull();
        writer.ExecuteTask.IsCompletedSuccessfully.ShouldBeTrue();
        string file = Path.Combine(_dir, $"decisions-{DateTime.UtcNow:yyyy-MM-dd}.jsonl");
        File.ReadAllLines(file).Length.ShouldBe(50);
    }

    [Fact]
    public async Task TryLog_AfterStop_ShouldReturnFalseNotThrow()
    {
        var options = Options.Create(new DecisionLogOptions { RootPath = _dir });
        using var writer = new DecisionLogWriter(options, NullLogger<DecisionLogWriter>.Instance);
        await writer.StartAsync(CancellationToken.None);
        await writer.StopAsync(CancellationToken.None);

        bool accepted = writer.TryLog(new DecisionLogEntry(
            DateTimeOffset.UtcNow, "ai-1", "late", "CommentCreated", DecisionOutcome.Executed));

        accepted.ShouldBeFalse();
    }

    [Fact]
    public async Task TryLog_WhenChannelFull_ShouldReturnFalse()
    {
        var options = Options.Create(new DecisionLogOptions { RootPath = _dir, QueueCapacity = 1 });
        using var writer = new DecisionLogWriter(options, NullLogger<DecisionLogWriter>.Instance);
        // NOT started: no pump consuming, so the second write must find the channel full.

        writer.TryLog(new DecisionLogEntry(
            DateTimeOffset.UtcNow, "ai-1", "a1", "CommentCreated", DecisionOutcome.Executed)).ShouldBeTrue();
        bool second = writer.TryLog(new DecisionLogEntry(
            DateTimeOffset.UtcNow, "ai-1", "a2", "CommentCreated", DecisionOutcome.Executed));

        second.ShouldBeFalse();
    }

    private static async Task WaitForFileLineAsync(string file)
    {
        for (int i = 0; i < 100; i++)
        {
            if (File.Exists(file) && new FileInfo(file).Length > 0) { return; }
            await Task.Delay(20);
        }
        throw new TimeoutException($"No line appeared in {file}.");
    }
}
