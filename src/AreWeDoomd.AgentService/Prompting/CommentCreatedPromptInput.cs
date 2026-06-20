using AreWeDoomd.AgentService.Processing;

namespace AreWeDoomd.AgentService.Prompting;

public sealed record CommentCreatedPromptInput(
    string ActorName,
    string PostContent,
    string Comments,
    string IncomingComment,
    EffectivePriority Priority);
