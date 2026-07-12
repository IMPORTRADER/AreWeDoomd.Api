namespace AreWeDoomd.AgentService.Decisions;

internal sealed record DecisionWire(
    IReadOnlyList<ActionWire>? Actions,
    string? Reasoning);
