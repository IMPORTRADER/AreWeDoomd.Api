using System.Threading.Channels;

namespace AreWeDoomd.AgentService.Processing;

public sealed class AgentEventQueue
{
    // Changed from DropOldest to Wait so that TryWrite (and therefore TryEnqueue)
    // returns FALSE when the channel is at capacity. This makes overflow visible:
    // the listener's reject branch fires, logs a Dropped decision-log entry, and
    // the new event is discarded without silently evicting an old one. Under all
    // BoundedChannelFullMode.Drop* variants, TryWrite always returns true — only
    // Wait mode makes TryWrite return false, which is what we need. Under overflow
    // we keep old events intact and reject new ones, with every drop logged.
    private readonly Channel<AgentEvent> _channel = Channel.CreateBounded<AgentEvent>(
        new BoundedChannelOptions(capacity: 100)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    public bool TryEnqueue(AgentEvent agentEvent)
    {
        return _channel.Writer.TryWrite(agentEvent);
    }

    public ValueTask<AgentEvent> DequeueAsync(CancellationToken ct)
    {
        return _channel.Reader.ReadAsync(ct);
    }
}
