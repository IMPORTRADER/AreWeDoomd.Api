namespace AreWeDoomd.AgentService.Context;

public interface IContextFetcher
{
    Task<PostContext?> FetchAsync(Guid postId, string agentUserId, CancellationToken ct);
}
