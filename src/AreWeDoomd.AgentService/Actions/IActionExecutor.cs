using AreWeDoomd.AgentService.Decisions;

namespace AreWeDoomd.AgentService.Actions;

public interface IActionExecutor
{
    Task ExecuteAsync(
        AgentDecision decision,
        Guid postId,
        Guid commentId,
        string actingUserId,
        CancellationToken ct);
}
