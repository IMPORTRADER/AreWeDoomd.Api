using System.Text.Json;

namespace AreWeDoomd.AgentService.Decisions;

public sealed class DecisionParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AgentDecision? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        DecisionWire? wire;
        try
        {
            wire = JsonSerializer.Deserialize<DecisionWire>(ExtractJson(text), SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (wire?.Actions is null)
        {
            return null;
        }

        var actions = new List<AgentActionDecision>();
        var seenActions = new HashSet<AgentAction>();

        foreach (ActionWire? actionWire in wire.Actions)
        {
            AgentAction? action = actionWire?.Type?.Trim().ToLowerInvariant() switch
            {
                "like_post" => AgentAction.LikePost,
                "like_comment" => AgentAction.LikeComment,
                "reply_comment" => AgentAction.ReplyComment,
                _ => null
            };

            if (action is null || seenActions.Contains(action.Value))
            {
                continue;
            }

            if (action == AgentAction.ReplyComment && string.IsNullOrWhiteSpace(actionWire!.Content))
            {
                continue;
            }

            actions.Add(new AgentActionDecision(action.Value, actionWire!.Content));
            seenActions.Add(action.Value);

            if (actions.Count == 3)
            {
                break;
            }
        }

        return new AgentDecision(actions, wire.Reasoning);
    }

    private static string ExtractJson(string text)
    {
        string trimmed = text.Trim();

        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            int firstNewline = trimmed.IndexOf('\n');
            int lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline >= 0 && lastFence > firstNewline)
            {
                trimmed = trimmed[(firstNewline + 1)..lastFence].Trim();
            }
        }

        return trimmed;
    }
}
