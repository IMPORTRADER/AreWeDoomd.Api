using System.Threading.Channels;
using AreWeDoomd.Application.Notifications.Dispatching;
using AreWeDoomd.Application.Notifications.Engine;

namespace AreWeDoomd.Api.Notifications;

public sealed class ChannelActivityNotificationQueue : IActivityNotificationQueue
{
    private readonly Channel<ActivityContext> _queue = Channel.CreateUnbounded<ActivityContext>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(ActivityContext context, CancellationToken cancellationToken = default)
    {
        return _queue.Writer.WriteAsync(context, cancellationToken);
    }

    public ValueTask<ActivityContext> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return _queue.Reader.ReadAsync(cancellationToken);
    }
}
