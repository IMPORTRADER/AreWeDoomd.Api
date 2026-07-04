using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.AgentService.Logging;

/// <summary>
/// Non-blocking JSONL decision log. TryLog enqueues; a single background pump
/// appends to daily files. Invariant: a log failure never stalls or fails
/// decision processing — TryLog never blocks and never throws.
/// </summary>
public sealed class DecisionLogWriter : BackgroundService, IDecisionLogWriter
{
    private readonly Channel<DecisionLogEntry> _channel;
    private readonly DecisionLogFileAppender _appender;
    private readonly DecisionLogOptions _options;
    private readonly ILogger<DecisionLogWriter> _logger;
    private long _rejectedCount;

    public DecisionLogWriter(IOptions<DecisionLogOptions> options, ILogger<DecisionLogWriter> logger)
    {
        _options = options.Value;
        _logger = logger;
        _appender = new DecisionLogFileAppender(_options.RootPath);
        _channel = Channel.CreateBounded<DecisionLogEntry>(new BoundedChannelOptions(_options.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true
        });
    }

    public bool TryLog(DecisionLogEntry entry)
    {
        try
        {
            if (_channel.Writer.TryWrite(entry))
            {
                return true;
            }

            long total = Interlocked.Increment(ref _rejectedCount);
            _logger.LogWarning(
                "Decision log channel full; entry for activity {ActivityId} dropped ({Total} rejected so far).",
                entry.ActivityId, total);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Decision log TryLog failed for activity {ActivityId}; entry dropped.", entry.ActivityId);
            return false;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _channel.Writer.TryComplete();
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.WhenAll(PumpAsync(stoppingToken), RetentionLoopAsync(stoppingToken));
    }

    private async Task PumpAsync(CancellationToken ct)
    {
        await foreach (DecisionLogEntry entry in _channel.Reader.ReadAllAsync(CancellationToken.None))
        {
            try
            {
                _appender.Append(entry);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to append decision log entry for activity {ActivityId}.", entry.ActivityId);
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
            int deleted = _appender.CleanupOldFiles(_options.RetentionDays);
            if (deleted > 0)
            {
                _logger.LogInformation("Decision log retention deleted {Count} expired file(s).", deleted);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Decision log retention cleanup failed.");
        }
    }
}
