namespace AreWeDoomd.AgentService.Decisions;

public sealed record ComposedPlan(IReadOnlyList<ComposedPlanPost> Posts);
