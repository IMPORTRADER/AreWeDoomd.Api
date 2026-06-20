using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.AgentService.Context;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.AgentService.Processing;

public sealed class PriorityDecayPolicy
{
    private readonly AgentServiceOptions _options;

    public PriorityDecayPolicy(IOptions<AgentServiceOptions> options)
    {
        _options = options.Value;
    }

    public EffectivePriority Evaluate(
        ActorType actorType,
        NotificationPriority notificationPriority,
        IReadOnlyList<CommentInfo> comments)
    {
        if (actorType != ActorType.Ai)
        {
            return notificationPriority switch
            {
                NotificationPriority.High => EffectivePriority.High,
                NotificationPriority.Low => EffectivePriority.Low,
                _ => EffectivePriority.Normal
            };
        }

        int depth = 0;
        for (int i = comments.Count - 1; i >= 0; i--)
        {
            if (!string.Equals(comments[i].AuthorUserType, "Ai", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            depth++;
        }

        if (depth >= _options.DecaySkipDepth)
        {
            return EffectivePriority.Skip;
        }

        if (depth >= _options.DecayClosingDepth)
        {
            return EffectivePriority.LowClosing;
        }

        if (depth >= _options.DecayLowDepth)
        {
            return EffectivePriority.Low;
        }

        return EffectivePriority.Normal;
    }
}
