using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;
using AreWeDoomd.Infrastructure.Common.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.Infrastructure.Common.Services;

/// <summary>
/// Non-blocking JSONL ops log for the API process. TryLog enqueues; a single
/// background pump appends to daily files. Invariant: a log failure never stalls
/// or fails the operation being logged — TryLog never blocks and never throws.
/// </summary>
public sealed class ApiAgentOpsLogWriter : BackgroundService, IAgentOpsLogger
{
    private readonly Channel<AgentOpsLogRecord> _channel;
    private readonly AgentOpsLogOptions _options;
    private readonly ILogger<ApiAgentOpsLogWriter> _logger;
    private long _rejectedCount;
    // Used by StartAsync to block until ExecuteAsync has been called and the worker tasks
    // are actually queued on the thread pool.  Needed because .NET 10's BackgroundService.StartAsync
    // does Task.Run(() => ExecuteAsync(...), _stoppingCts.Token): if StopAsync cancels the token
    // before that Task.Run gets a thread, _executeTask is pre-cancelled and the drain never runs.
    private readonly TaskCompletionSource _executeCalled =
        new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ApiAgentOpsLogWriter(IOptions<AgentOpsLogOptions> options, ILogger<ApiAgentOpsLogWriter> logger)
    {
        _options = options.Value;
        _logger = logger;
        _channel = Channel.CreateBounded<AgentOpsLogRecord>(new BoundedChannelOptions(_options.QueueCapacity)
        {
            // Wait mode: TryWrite returns false when full (Drop* modes return true and discard silently). TryLog stays non-blocking — WriteAsync is never used.
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });
    }

    public bool TryLog(AgentOpsLogRecord entry)
    {
        try
        {
            if (_channel.Writer.TryWrite(entry))
            {
                return true;
            }

            long total = Interlocked.Increment(ref _rejectedCount);
            _logger.LogWarning(
                "Agent ops log channel full; entry dropped ({Total} rejected so far).",
                total);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Agent ops log TryLog failed; entry dropped.");
            return false;
        }
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken);
        // Wait until ExecuteAsync has been called so the pump/retention tasks are on the thread
        // pool before we return.  This guarantees StopAsync cannot cancel _stoppingCts before
        // the Task.Run inside BackgroundService.StartAsync has had a chance to call ExecuteAsync.
        await _executeCalled.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.Writer.TryComplete();
        await base.StopAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Task.Run detaches the loops from any ambient SynchronizationContext (e.g. test frameworks);
        // continuations must run on the thread pool or StopAsync's drain can be bypassed.
        var pumpTask = Task.Run(() => PumpAsync(stoppingToken), CancellationToken.None);
        var retentionTask = Task.Run(() => RetentionLoopAsync(stoppingToken), CancellationToken.None);
        // Signal StartAsync that the worker tasks are queued; it is now safe for StopAsync to run.
        _executeCalled.TrySetResult();
        return Task.WhenAll(pumpTask, retentionTask);
    }

    private async Task PumpAsync(CancellationToken ct)
    {
        await foreach (AgentOpsLogRecord entry in _channel.Reader.ReadAllAsync(CancellationToken.None))
        {
            try
            {
                Append(entry);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to append agent ops log entry.");
            }
        }
    }

    private async Task RetentionLoopAsync(CancellationToken ct)
    {
        RunCleanup();
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                RunCleanup();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private void RunCleanup()
    {
        try
        {
            int deleted = CleanupOldFiles(_options.RetentionDays);
            if (deleted > 0)
            {
                _logger.LogInformation("Agent ops log retention deleted {Count} expired file(s).", deleted);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Agent ops log retention cleanup failed.");
        }
    }

    private void Append(AgentOpsLogRecord entry)
    {
        Directory.CreateDirectory(_options.RootPath);
        string path = Path.Combine(
            _options.RootPath,
            $"agent-logs-api-{entry.Ts.UtcDateTime:yyyy-MM-dd}.jsonl");

        using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.WriteLine(JsonSerializer.Serialize(entry, JsonOptions));
    }

    private int CleanupOldFiles(int retentionDays)
    {
        if (!Directory.Exists(_options.RootPath))
        {
            return 0;
        }

        DateTime cutoff = DateTime.UtcNow.Date.AddDays(-retentionDays);
        int deleted = 0;

        foreach (string file in Directory.EnumerateFiles(_options.RootPath, "agent-logs-api-*.jsonl"))
        {
            string stem = Path.GetFileNameWithoutExtension(file);
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
