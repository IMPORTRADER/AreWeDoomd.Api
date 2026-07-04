using AreWeDoomd.Infrastructure.Common.Options;
using AreWeDoomd.Infrastructure.Common.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Infrastructure;

public sealed class FileSessionLogReaderTests : IDisposable
{
    // RootPath is set to a sub-folder so its parent becomes the shared-volume root.
    // ai-sessions lives at {parent}/ai-sessions, mirroring the real AgentService layout.
    private readonly string _volumeRoot =
        Path.Combine(Path.GetTempPath(), "awd-sessions-" + Guid.NewGuid().ToString("N"));

    private string AgentDecisionsRoot => Path.Combine(_volumeRoot, "agent-decisions");
    private string SessionsRoot => Path.Combine(_volumeRoot, "ai-sessions");

    public void Dispose()
    {
        if (Directory.Exists(_volumeRoot))
        {
            Directory.Delete(_volumeRoot, recursive: true);
        }
    }

    private FileSessionLogReader CreateReader()
    {
        var options = Options.Create(new DecisionLogOptions { RootPath = AgentDecisionsRoot });
        return new FileSessionLogReader(options, NullLogger<FileSessionLogReader>.Instance);
    }

    private void WriteSessionFile(string sessionRef, string content)
    {
        var fullPath = Path.Combine(SessionsRoot, sessionRef.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    // ── Valid read ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ReadAsync_ValidRef_ReturnsFileContent()
    {
        const string sessionRef = "2026-07-04/act-1_attempt1_123105.txt";
        const string expected = "PROMPT\n---\nRESPONSE";
        WriteSessionFile(sessionRef, expected);

        var reader = CreateReader();
        var result = await reader.ReadAsync(sessionRef, CancellationToken.None);

        result.ShouldBe(expected);
    }

    // ── Regex defense: traversal and bad chars ──────────────────────────────

    [Fact]
    public async Task ReadAsync_DoubleDotTraversal_ReturnsNull()
    {
        var reader = CreateReader();
        var result = await reader.ReadAsync("../../etc/x.txt", CancellationToken.None);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ReadAsync_TraversalInMiddleSegment_ReturnsNull()
    {
        var reader = CreateReader();
        var result = await reader.ReadAsync("2026-01-01/../x.txt", CancellationToken.None);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ReadAsync_AbsolutePath_ReturnsNull()
    {
        var reader = CreateReader();
        // On Windows and Linux the leading slash/drive letter will fail the regex date prefix check
        var result = await reader.ReadAsync("/etc/passwd", CancellationToken.None);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ReadAsync_BadCharactersInFilename_ReturnsNull()
    {
        var reader = CreateReader();
        var result = await reader.ReadAsync("2026-01-01/bad$chars.txt", CancellationToken.None);
        result.ShouldBeNull();
    }

    // ── Missing file ────────────────────────────────────────────────────────

    [Fact]
    public async Task ReadAsync_MissingFile_ReturnsNull()
    {
        var reader = CreateReader();
        var result = await reader.ReadAsync("2026-07-04/nonexistent_file.txt", CancellationToken.None);
        result.ShouldBeNull();
    }

    // ── 256 KB cap ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ReadAsync_OversizedFile_TruncatesWithMarker()
    {
        const string sessionRef = "2026-07-04/bigfile_log.txt";
        // Write 300 KB of 'A'
        var oversizedContent = new string('A', 300 * 1024);
        WriteSessionFile(sessionRef, oversizedContent);

        var reader = CreateReader();
        var result = await reader.ReadAsync(sessionRef, CancellationToken.None);

        result.ShouldNotBeNull();
        result!.Length.ShouldBeLessThan(oversizedContent.Length);
        result.ShouldEndWith("[...truncated]");
    }
}
