namespace AreWeDoomd.AgentService.Ai;

public interface IChatProvider
{
    string Name { get; }

    Task<ChatResult> CompleteAsync(ChatRequest request, CancellationToken ct);
}
