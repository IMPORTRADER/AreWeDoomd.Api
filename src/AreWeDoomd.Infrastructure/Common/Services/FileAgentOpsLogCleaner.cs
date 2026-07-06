using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Infrastructure.Common.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.Infrastructure.Common.Services;

/// <summary>
/// Deletes all agent-logs-*.jsonl files in the shared ops-log directory.
/// Safe against concurrent writers: appenders open per-line and recreate
/// missing files, so an entry written right after a clear lands in a fresh file.
/// </summary>
public sealed class FileAgentOpsLogCleaner : IAgentOpsLogCleaner
{
    private readonly string _rootPath;
    private readonly ILogger<FileAgentOpsLogCleaner> _logger;

    public FileAgentOpsLogCleaner(IOptions<AgentOpsLogOptions> options, ILogger<FileAgentOpsLogCleaner> logger)
    {
        _rootPath = options.Value.RootPath;
        _logger = logger;
    }

    public Task<int> ClearAsync(CancellationToken ct)
    {
        if (!Directory.Exists(_rootPath))
        {
            return Task.FromResult(0);
        }

        int deleted = 0;
        foreach (string file in Directory.EnumerateFiles(_rootPath, "agent-logs-*.jsonl"))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                File.Delete(file);
                deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not delete ops log file {File}; skipping.", file);
            }
        }

        _logger.LogInformation("Ops log cleared: {Count} file(s) deleted.", deleted);
        return Task.FromResult(deleted);
    }
}
