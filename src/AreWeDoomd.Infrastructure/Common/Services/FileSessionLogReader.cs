using System.Text.RegularExpressions;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Infrastructure.Common.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.Infrastructure.Common.Services;

public sealed class FileSessionLogReader : ISessionLogReader
{
    private const int MaxBytes = 256 * 1024; // 256 KB
    private const string TruncationMarker = "[...truncated]";

    private static readonly Regex RefPattern =
        new(@"^\d{4}-\d{2}-\d{2}/[A-Za-z0-9_\-]+\.txt$", RegexOptions.Compiled);

    private readonly string _sessionsRoot;
    private readonly ILogger<FileSessionLogReader> _logger;

    public FileSessionLogReader(IOptions<DecisionLogOptions> options, ILogger<FileSessionLogReader> logger)
    {
        // agent-decisions and ai-sessions are siblings under the shared volume root.
        var absoluteDecisionsRoot = Path.GetFullPath(options.Value.RootPath);
        var volumeRoot = Path.GetDirectoryName(absoluteDecisionsRoot)
            ?? throw new InvalidOperationException("Cannot determine parent directory of DecisionLog:RootPath.");
        _sessionsRoot = Path.Combine(volumeRoot, "ai-sessions");
        _logger = logger;
    }

    public async Task<string?> ReadAsync(string sessionRef, CancellationToken ct)
    {
        // Defense 1: strict regex — only allows yyyy-MM-dd/SafeName.txt
        if (!RefPattern.IsMatch(sessionRef))
        {
            return null;
        }

        // Defense 2: resolved absolute path must stay inside _sessionsRoot
        var resolvedPath = Path.GetFullPath(Path.Combine(_sessionsRoot, sessionRef));
        var sessionsRootWithSep = _sessionsRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!resolvedPath.StartsWith(sessionsRootWithSep, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Session log path traversal attempt blocked for ref '{Ref}'", sessionRef);
            return null;
        }

        if (!File.Exists(resolvedPath))
        {
            return null;
        }

        try
        {
            using var stream = new FileStream(resolvedPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            // Read up to MaxBytes + 1 to detect whether truncation is needed
            var buffer = new byte[MaxBytes + 1];
            int bytesRead = 0;
            int chunk;
            while (bytesRead < buffer.Length &&
                   (chunk = await stream.ReadAsync(buffer.AsMemory(bytesRead, buffer.Length - bytesRead), ct)) > 0)
            {
                bytesRead += chunk;
            }

            bool truncated = bytesRead > MaxBytes;
            var content = System.Text.Encoding.UTF8.GetString(buffer, 0, Math.Min(bytesRead, MaxBytes));

            return truncated ? content + TruncationMarker : content;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading session log file '{Path}'", resolvedPath);
            return null;
        }
    }
}
