using System.Globalization;
using System.Text.Json;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Infrastructure.Common.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.Infrastructure.Common.Services;

public sealed class FileDecisionLogReader : IDecisionLogReader
{
    private readonly string _rootPath;
    private readonly ILogger<FileDecisionLogReader> _logger;
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    public FileDecisionLogReader(IOptions<DecisionLogOptions> options, ILogger<FileDecisionLogReader> logger)
    {
        _rootPath = options.Value.RootPath;
        _logger = logger;
    }

    public async Task<DecisionLogPage> ReadAsync(
        DecisionLogFilter filter, string? cursor, int pageSize, CancellationToken ct)
    {
        if (!Directory.Exists(_rootPath))
        {
            return new DecisionLogPage([], null, false, LogAvailable: false);
        }

        var gathered = new List<DecisionLogRecord>();

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

                var dateItems = await ReadAndFilterDateFileAsync(date, filter, ct);

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

            return new DecisionLogPage(gathered, nextCursor, HasMore: nextCursor != null, LogAvailable: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error reading decision log from {RootPath}", _rootPath);
            return new DecisionLogPage(gathered, null, HasMore: false, LogAvailable: true);
        }
    }

    public async Task<DecisionLogDailyStats?> GetDailyStatsAsync(
        DateOnly dateUtc, DateTimeOffset nowUtc, CancellationToken ct)
    {
        if (!Directory.Exists(_rootPath))
        {
            return null;
        }

        var filePath = Path.Combine(_rootPath, $"decisions-{dateUtc:yyyy-MM-dd}.jsonl");
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            int total = 0;
            int executed = 0;
            int dropped = 0;
            int failed = 0;
            int actionsLastHour = 0;
            var cutoff = nowUtc.AddHours(-1);

            string? line;
            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                DecisionLogRecord? record = null;
                try
                {
                    record = JsonSerializer.Deserialize<DecisionLogRecord>(line, JsonOptions);
                }
                catch
                {
                    continue;
                }

                if (record == null)
                {
                    continue;
                }

                total++;

                if (string.Equals(record.Outcome, "executed", StringComparison.OrdinalIgnoreCase))
                {
                    executed++;
                    if (record.Ts >= cutoff)
                    {
                        actionsLastHour++;
                    }
                }
                else if (string.Equals(record.Outcome, "dropped", StringComparison.OrdinalIgnoreCase))
                {
                    dropped++;
                }
                else if (string.Equals(record.Outcome, "llm_failed", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(record.Outcome, "action_failed", StringComparison.OrdinalIgnoreCase))
                {
                    failed++;
                }
            }

            return new DecisionLogDailyStats(dateUtc, total, executed, dropped, failed, actionsLastHour);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading decision log stats for date {Date}", dateUtc);
            return null;
        }
    }

    private List<DateOnly> BuildCandidateDates(DecisionLogFilter filter, DateOnly? cursorDate)
    {
        var result = new List<DateOnly>();

        try
        {
            var files = Directory.GetFiles(_rootPath, "decisions-*.jsonl");
            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var datePart = name["decisions-".Length..];
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

                result.Add(date);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error enumerating decision log files in {RootPath}", _rootPath);
        }

        result.Sort((a, b) => b.CompareTo(a));
        return result;
    }

    private async Task<List<DecisionLogRecord>> ReadAndFilterDateFileAsync(
        DateOnly date, DecisionLogFilter filter, CancellationToken ct)
    {
        var filePath = Path.Combine(_rootPath, $"decisions-{date:yyyy-MM-dd}.jsonl");
        var records = new List<DecisionLogRecord>();

        if (!File.Exists(filePath))
        {
            return records;
        }

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            var lines = new List<string>();
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                lines.Add(line);
            }

            foreach (var l in lines)
            {
                if (string.IsNullOrWhiteSpace(l))
                {
                    continue;
                }

                DecisionLogRecord? record = null;
                try
                {
                    record = JsonSerializer.Deserialize<DecisionLogRecord>(l, JsonOptions);
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

            records.Reverse();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error reading decision log file for date {Date}", date);
        }

        return records;
    }

    private static bool MatchesFilter(DecisionLogRecord record, DecisionLogFilter filter)
    {
        if (filter.AiUserId != null
            && !string.Equals(record.AiUserId, filter.AiUserId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (filter.Action != null
            && !string.Equals(record.Action, filter.Action, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (filter.Outcome != null
            && !string.Equals(record.Outcome, filter.Outcome, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
