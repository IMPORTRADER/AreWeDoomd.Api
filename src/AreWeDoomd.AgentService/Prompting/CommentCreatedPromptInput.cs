using AreWeDoomd.AgentService.Processing;

namespace AreWeDoomd.AgentService.Prompting;

public sealed record CommentCreatedPromptInput(
    string ActorName,
    string PostContent,
    string CommentThread,
    string IncomingComment,
    EffectivePriority Priority);
