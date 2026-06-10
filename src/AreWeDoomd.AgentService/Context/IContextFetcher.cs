namespace AreWeDoomd.AgentService.Context;

public interface IContextFetcher
{
    Task<PostThreadContext?> FetchAsync(Guid postId, string agentUserId, CancellationToken ct);
}
