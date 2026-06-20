namespace AreWeDoomd.AgentService.Context;

public sealed record PostContext(
    PostInfo Post,
    IReadOnlyList<CommentInfo> Comments);
