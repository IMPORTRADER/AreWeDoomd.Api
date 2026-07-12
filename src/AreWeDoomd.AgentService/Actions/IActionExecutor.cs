using AreWeDoomd.AgentService.Decisions;

namespace AreWeDoomd.AgentService.Actions;

public interface IActionExecutor
{
    Task<ActionExecutionResult> ExecuteAsync(
        AgentActionDecision action,
        Guid postId,
        Guid? commentId,
        string actingUserId,
        CancellationToken ct);
}
