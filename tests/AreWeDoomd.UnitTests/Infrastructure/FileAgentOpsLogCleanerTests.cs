using AreWeDoomd.Infrastructure.Common.Options;
using AreWeDoomd.Infrastructure.Common.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AreWeDoomd.UnitTests.Infrastructure;

public sealed class FileAgentOpsLogCleanerTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(), "ops-cleaner-tests-" + Guid.NewGuid().ToString("N"));

    private FileAgentOpsLogCleaner CreateCleaner()
    {
        var options = Options.Create(new AgentOpsLogOptions { RootPath = _tempDir });
        return new FileAgentOpsLogCleaner(options, NullLogger<FileAgentOpsLogCleaner>.Instance);
    }

    [Fact]
    public async Task ClearAsync_DeletesAllOpsLogFiles_LeavesOtherFiles()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(Path.Combine(_tempDir, "agent-logs-agentservice-2026-07-06.jsonl"), "{}");
        File.WriteAllText(Path.Combine(_tempDir, "agent-logs-api-2026-07-06.jsonl"), "{}");
        File.WriteAllText(Path.Combine(_tempDir, "unrelated.txt"), "keep");

        int deleted = await CreateCleaner().ClearAsync(CancellationToken.None);

        Assert.Equal(2, deleted);
        Assert.Empty(Directory.GetFiles(_tempDir, "agent-logs-*.jsonl"));
        Assert.True(File.Exists(Path.Combine(_tempDir, "unrelated.txt")));
    }

    [Fact]
    public async Task ClearAsync_MissingDirectory_ReturnsZero()
    {
        int deleted = await CreateCleaner().ClearAsync(CancellationToken.None);

        Assert.Equal(0, deleted);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
