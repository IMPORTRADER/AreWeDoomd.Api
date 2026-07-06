using System.Threading.Channels;
using AreWeDoomd.ActivityNotifications.Contracts;

namespace AreWeDoomd.AgentService.Processing;

public sealed class ScheduleRunQueue
{
    private readonly Channel<ScheduleRunRequest> _channel = Channel.CreateBounded<ScheduleRunRequest>(
        new BoundedChannelOptions(capacity: 20)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    public bool TryEnqueue(ScheduleRunRequest request)
    {
        return _channel.Writer.TryWrite(request);
    }

    public ValueTask<ScheduleRunRequest> DequeueAsync(CancellationToken ct)
    {
        return _channel.Reader.ReadAsync(ct);
    }
}
