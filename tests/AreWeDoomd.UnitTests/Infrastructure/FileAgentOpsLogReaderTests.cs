using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Infrastructure.Common.Options;
using AreWeDoomd.Infrastructure.Common.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AreWeDoomd.UnitTests.Infrastructure;

public sealed class FileAgentOpsLogReaderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(), "ops-reader-tests-" + Guid.NewGuid().ToString("N"));

    private FileAgentOpsLogReader CreateReader()
    {
        var options = Options.Create(new AgentOpsLogOptions { RootPath = _tempDir });
        return new FileAgentOpsLogReader(options, NullLogger<FileAgentOpsLogReader>.Instance);
    }

    private void WriteFile(string fileName, params string[] lines)
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllLines(Path.Combine(_tempDir, fileName), lines);
    }

    private static string Line(string ts, string level, string source, string message, string? aiUserId = null)
    {
        string user = aiUserId is null ? "" : $",\"aiUserId\":\"{aiUserId}\"";
        return $"{{\"ts\":\"{ts}\",\"level\":\"{level}\",\"source\":\"{source}\",\"message\":\"{message}\"{user}}}";
    }

    [Fact]
    public async Task ReadAsync_MergesBothProcessFiles_NewestFirst()
    {
        WriteFile("agent-logs-agentservice-2026-07-06.jsonl",
            Line("2026-07-06T10:00:00Z", "info", "pipeline", "svc-early"),
            Line("2026-07-06T12:00:00Z", "info", "llm_provider", "svc-late"));
        WriteFile("agent-logs-api-2026-07-06.jsonl",
            Line("2026-07-06T11:00:00Z", "info", "admin", "api-mid"));

        var page = await CreateReader().ReadAsync(new AgentOpsLogFilter(), null, 10, CancellationToken.None);

        Assert.True(page.LogAvailable);
        Assert.Equal(["svc-late", "api-mid", "svc-early"], page.Items.Select(i => i.Message).ToArray());
    }

    [Fact]
    public async Task ReadAsync_FiltersByLevelSourceAndAiUser()
    {
        WriteFile("agent-logs-agentservice-2026-07-06.jsonl",
            Line("2026-07-06T10:00:00Z", "error", "llm_provider", "boom", aiUserId: "u1"),
            Line("2026-07-06T10:01:00Z", "info", "llm_provider", "fine", aiUserId: "u1"),
            Line("2026-07-06T10:02:00Z", "error", "actions", "other-source", aiUserId: "u2"));

        var page = await CreateReader().ReadAsync(
            new AgentOpsLogFilter(Level: "error", Source: "llm_provider", AiUserId: "u1"),
            null, 10, CancellationToken.None);

        Assert.Single(page.Items);
        Assert.Equal("boom", page.Items[0].Message);
    }

    [Fact]
    public async Task ReadAsync_PagesAcrossDatesWithCursor()
    {
        WriteFile("agent-logs-api-2026-07-06.jsonl",
            Line("2026-07-06T10:00:00Z", "info", "admin", "today-1"),
            Line("2026-07-06T11:00:00Z", "info", "admin", "today-2"));
        WriteFile("agent-logs-api-2026-07-05.jsonl",
            Line("2026-07-05T10:00:00Z", "info", "admin", "yesterday-1"));

        var reader = CreateReader();
        var page1 = await reader.ReadAsync(new AgentOpsLogFilter(), null, 2, CancellationToken.None);
        Assert.True(page1.HasMore);
        Assert.Equal(["today-2", "today-1"], page1.Items.Select(i => i.Message).ToArray());

        var page2 = await reader.ReadAsync(new AgentOpsLogFilter(), page1.NextCursor, 2, CancellationToken.None);
        Assert.False(page2.HasMore);
        Assert.Equal(["yesterday-1"], page2.Items.Select(i => i.Message).ToArray());
    }

    [Fact]
    public async Task ReadAsync_MissingDirectory_ReportsLogUnavailable()
    {
        var page = await CreateReader().ReadAsync(new AgentOpsLogFilter(), null, 10, CancellationToken.None);

        Assert.False(page.LogAvailable);
        Assert.Empty(page.Items);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
