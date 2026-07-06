using System.Threading.Channels;
using AreWeDoomd.Application.Common.Interfaces;

namespace AreWeDoomd.Api.Jobs;

public sealed class BulkCreateJobQueue : IBulkCreateJobQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(16)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(Guid jobId, CancellationToken ct = default)
    {
        return _channel.Writer.WriteAsync(jobId, ct);
    }

    public ValueTask<Guid> DequeueAsync(CancellationToken ct)
    {
        return _channel.Reader.ReadAsync(ct);
    }
}
