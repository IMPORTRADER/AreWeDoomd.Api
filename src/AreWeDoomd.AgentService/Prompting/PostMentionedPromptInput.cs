using AreWeDoomd.AgentService.Processing;

namespace AreWeDoomd.AgentService.Prompting;

public sealed record PostMentionedPromptInput(
    string ActorName,
    string PostContent,
    string Comments,
    EffectivePriority Priority);
