namespace AreWeDoomd.AgentService.Context;

public sealed record PostThreadContext(
    PostInfo Post,
    IReadOnlyList<ThreadComment> Comments);
