using System.Globalization;
using System.Text;

namespace AreWeDoomd.AgentService.Logging;

/// <summary>
/// Synchronous file operations for the ops log: daily-file append and retention
/// cleanup. Not thread-safe by itself — AgentOpsLogWriter's single pump is the
/// only caller at runtime. Each process writes its own file (process-name
/// segment in the filename) so API and AgentService never contend on a file.
/// </summary>
public sealed class AgentOpsLogFileAppender
{
    private const string FilePrefix = "agent-logs-";
    private const string FileSuffix = ".jsonl";

    private readonly string _rootPath;
    private readonly string _processName;

    public AgentOpsLogFileAppender(string rootPath, string processName)
    {
        _rootPath = rootPath;
        _processName = processName;
    }

    public void Append(AgentOpsLogEntry entry)
    {
        Directory.CreateDirectory(_rootPath);
        string path = Path.Combine(
            _rootPath,
            $"{FilePrefix}{_processName}-{entry.Ts.UtcDateTime:yyyy-MM-dd}{FileSuffix}");

        using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.WriteLine(AgentOpsLogSerializer.Serialize(entry));
    }

    public int CleanupOldFiles(int retentionDays)
    {
        if (!Directory.Exists(_rootPath))
        {
            return 0;
        }

        DateTime cutoff = DateTime.UtcNow.Date.AddDays(-retentionDays);
        int deleted = 0;

        foreach (string file in Directory.EnumerateFiles(_rootPath, $"{FilePrefix}*{FileSuffix}"))
        {
            string stem = Path.GetFileName(file)[FilePrefix.Length..^FileSuffix.Length];
            if (stem.Length < 10)
            {
                continue;
            }

            string datePart = stem[^10..];
            if (DateTime.TryParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime fileDate)
                && fileDate.Date < cutoff)
            {
                File.Delete(file);
                deleted++;
            }
        }

        return deleted;
    }
}
