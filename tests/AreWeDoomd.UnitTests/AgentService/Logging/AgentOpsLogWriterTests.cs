using AreWeDoomd.AgentService.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AreWeDoomd.UnitTests.AgentService.Logging;

public sealed class AgentOpsLogWriterTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(), "ops-log-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task TryLog_WritesEntryToDailyProcessFile()
    {
        var options = Options.Create(new AgentOpsLogOptions { RootPath = _tempDir });
        var writer = new AgentOpsLogWriter(options, NullLogger<AgentOpsLogWriter>.Instance);
        await writer.StartAsync(CancellationToken.None);

        var ts = new DateTimeOffset(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);
        bool accepted = writer.TryLog(new AgentOpsLogEntry(
            ts, AgentOpsLogLevel.Info, AgentOpsLogSource.LlmProvider,
            "Requesting Gemini (gemini-2.0-flash), attempt 1",
            AiUserId: "user-1", ActivityId: "act-1"));

        await writer.StopAsync(CancellationToken.None);

        Assert.True(accepted);
        string path = Path.Combine(_tempDir, "agent-logs-agentservice-2026-07-06.jsonl");
        Assert.True(File.Exists(path));
        string line = (await File.ReadAllLinesAsync(path)).Single();
        Assert.Contains("\"level\":\"info\"", line);
        Assert.Contains("\"source\":\"llm_provider\"", line);
        Assert.Contains("Requesting Gemini", line);
        Assert.DoesNotContain("aiUsername", line); // null props omitted
    }

    [Fact]
    public async Task TryLog_NeverThrows_WhenChannelCompleted()
    {
        var options = Options.Create(new AgentOpsLogOptions { RootPath = _tempDir });
        var writer = new AgentOpsLogWriter(options, NullLogger<AgentOpsLogWriter>.Instance);
        await writer.StartAsync(CancellationToken.None);
        await writer.StopAsync(CancellationToken.None);

        bool accepted = writer.TryLog(new AgentOpsLogEntry(
            DateTimeOffset.UtcNow, AgentOpsLogLevel.Error, AgentOpsLogSource.Pipeline, "late"));

        Assert.False(accepted);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
