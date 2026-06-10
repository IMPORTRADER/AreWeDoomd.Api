using System.Threading.Channels;

namespace AreWeDoomd.AgentService.Processing;

public sealed class AgentEventQueue
{
    private readonly Channel<AgentEvent> _channel = Channel.CreateBounded<AgentEvent>(
        new BoundedChannelOptions(capacity: 100)
        {
            FullMode = BoundedChannelFullMode.DropOldest
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
