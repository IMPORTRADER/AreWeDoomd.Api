namespace AreWeDoomd.AgentService.Actions;

public sealed record ActionExecutionResult(ActionExecutionOutcome Outcome, string? ErrorDetail = null);
