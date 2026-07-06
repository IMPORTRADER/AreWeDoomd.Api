using System.Globalization;
using System.Text.Json;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Infrastructure.Common.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.Infrastructure.Common.Services;

public sealed class FileAgentOpsLogReader : IAgentOpsLogReader
{
    private readonly string _rootPath;
    private readonly ILogger<FileAgentOpsLogReader> _logger;
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    public FileAgentOpsLogReader(IOptions<AgentOpsLogOptions> options, ILogger<FileAgentOpsLogReader> logger)
    {
        _rootPath = options.Value.RootPath;
        _logger = logger;
    }

    public async Task<AgentOpsLogPage> ReadAsync(
        AgentOpsLogFilter filter, string? cursor, int pageSize, CancellationToken ct)
    {
        if (!Directory.Exists(_rootPath))
        {
            return new AgentOpsLogPage([], null, false, LogAvailable: false);
        }

        var gathered = new List<AgentOpsLogRecord>();

        try
        {
            DateOnly? cursorDate = null;
            int cursorSkip = 0;

            if (!string.IsNullOrEmpty(cursor))
            {
                var colonIdx = cursor.IndexOf(':');
                if (colonIdx > 0
                    && DateOnly.TryParseExact(cursor[..colonIdx], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate)
                    && int.TryParse(cursor[(colonIdx + 1)..], out int parsedSkip))
                {
                    cursorDate = parsedDate;
                    cursorSkip = parsedSkip;
                }
            }

            var candidateDates = BuildCandidateDates(filter, cursorDate);

            int remaining = pageSize;
            string? nextCursor = null;

            for (int i = 0; i < candidateDates.Count && remaining > 0; i++)
            {
                var date = candidateDates[i];
                int skip = (cursorDate.HasValue && date == cursorDate.Value) ? cursorSkip : 0;

                var dateItems = await ReadAndFilterDateAsync(date, filter, ct);

                int available = dateItems.Count - skip;
                if (available <= 0)
                {
                    continue;
                }

                int toTake = Math.Min(available, remaining);
                gathered.AddRange(dateItems.Skip(skip).Take(toTake));
                remaining -= toTake;

                if (remaining == 0)
                {
                    int alreadyServedInDate = skip + toTake;
                    if (alreadyServedInDate < dateItems.Count)
                    {
                        nextCursor = $"{date:yyyy-MM-dd}:{alreadyServedInDate}";
                    }
                    else if (i + 1 < candidateDates.Count)
                    {
                        nextCursor = $"{candidateDates[i + 1]:yyyy-MM-dd}:0";
                    }
                    break;
                }
            }

            return new AgentOpsLogPage(gathered, nextCursor, HasMore: nextCursor != null, LogAvailable: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error reading agent ops log from {RootPath}", _rootPath);
            return new AgentOpsLogPage(gathered, null, HasMore: false, LogAvailable: true);
        }
    }

    private List<DateOnly> BuildCandidateDates(AgentOpsLogFilter filter, DateOnly? cursorDate)
    {
        var dates = new HashSet<DateOnly>();

        try
        {
            var files = Directory.GetFiles(_rootPath, "agent-logs-*.jsonl");
            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (name.Length < "agent-logs-".Length + 10)
                {
                    continue;
                }

                var datePart = name[^10..];
                if (!DateOnly.TryParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    continue;
                }

                if (filter.FromUtc.HasValue && date < filter.FromUtc.Value)
                {
                    continue;
                }

                if (filter.ToUtc.HasValue && date > filter.ToUtc.Value)
                {
                    continue;
                }

                if (cursorDate.HasValue && date > cursorDate.Value)
                {
                    continue;
                }

                dates.Add(date);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error enumerating agent ops log files in {RootPath}", _rootPath);
        }

        var result = dates.ToList();
        result.Sort((a, b) => b.CompareTo(a));
        return result;
    }

    private async Task<List<AgentOpsLogRecord>> ReadAndFilterDateAsync(
        DateOnly date, AgentOpsLogFilter filter, CancellationToken ct)
    {
        var records = new List<AgentOpsLogRecord>();

        var matchingFiles = Directory.GetFiles(_rootPath, $"agent-logs-*-{date:yyyy-MM-dd}.jsonl");

        foreach (var filePath in matchingFiles)
        {
            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);

                string? line;
                while ((line = await reader.ReadLineAsync(ct)) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    AgentOpsLogRecord? record = null;
                    try
                    {
                        record = JsonSerializer.Deserialize<AgentOpsLogRecord>(line, JsonOptions);
                    }
                    catch
                    {
                        continue;
                    }

                    if (record == null)
                    {
                        continue;
                    }

                    if (!MatchesFilter(record, filter))
                    {
                        continue;
                    }

                    records.Add(record);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reading agent ops log file {FilePath}", filePath);
            }
        }

        records = records.OrderByDescending(r => r.Ts).ToList();
        return records;
    }

    private static bool MatchesFilter(AgentOpsLogRecord record, AgentOpsLogFilter filter)
    {
        if (filter.Level != null
            && !string.Equals(record.Level, filter.Level, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (filter.Source != null
            && !string.Equals(record.Source, filter.Source, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (filter.AiUserId != null
            && !string.Equals(record.AiUserId, filter.AiUserId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
