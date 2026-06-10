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

        if (wire is null)
        {
            return null;
        }

        AgentAction? action = wire.Action?.Trim().ToLowerInvariant() switch
        {
            "reply_comment" => AgentAction.ReplyComment,
            "like_comment" => AgentAction.LikeComment,
            "ignore" => AgentAction.Ignore,
            _ => null
        };

        if (action is null)
        {
            return null;
        }

        if (action == AgentAction.ReplyComment && string.IsNullOrWhiteSpace(wire.Content))
        {
            return null;
        }

        return new AgentDecision(action.Value, wire.Content, wire.Reasoning);
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
