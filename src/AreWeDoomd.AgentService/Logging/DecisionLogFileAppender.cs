using System.Globalization;
using System.Text;

namespace AreWeDoomd.AgentService.Logging;

/// <summary>
/// Synchronous file operations for the decision log: daily-file append and
/// retention cleanup. Not thread-safe by itself — DecisionLogWriter's single
/// pump is the only caller at runtime.
/// </summary>
public sealed class DecisionLogFileAppender
{
    private const string FilePrefix = "decisions-";
    private const string FileSuffix = ".jsonl";

    private readonly string _rootPath;

    public DecisionLogFileAppender(string rootPath)
    {
        _rootPath = rootPath;
    }

    public void Append(DecisionLogEntry entry)
    {
        Directory.CreateDirectory(_rootPath);
        string path = Path.Combine(
            _rootPath,
            $"{FilePrefix}{entry.Ts.UtcDateTime:yyyy-MM-dd}{FileSuffix}");

        using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.WriteLine(DecisionLogSerializer.Serialize(entry));
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
            string datePart = Path.GetFileName(file)[FilePrefix.Length..^FileSuffix.Length];
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
