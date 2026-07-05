using System.Text.Json;

namespace AreWeDoomd.AgentService.Decisions;

public sealed class DailyPostPlanParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlyList<ScoredAccount>? ParseScores(string? text, IReadOnlySet<Guid> expectedRunItemIds)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        ScoreWire? wire;
        try
        {
            wire = JsonSerializer.Deserialize<ScoreWire>(ExtractJson(text), SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (wire?.Accounts is null)
        {
            return null;
        }

        var results = new List<ScoredAccount>();
        foreach (var account in wire.Accounts)
        {
            // runItemId echo doğrulaması: LLM satır atlarsa/uydurursa skor başka
            // hesaba sessizce yazılmasın — bilinmeyen id'ler düşer.
            if (!Guid.TryParse(account.RunItemId, out var runItemId) || !expectedRunItemIds.Contains(runItemId))
            {
                continue;
            }

            results.Add(new ScoredAccount(
                runItemId,
                account.Reasoning ?? string.Empty,
                Math.Clamp(account.DesireScore, 0, 100),
                Math.Clamp(account.HypotheticalPostCount, 1, 10)));
        }

        return results;
    }

    public ComposedPlan? ParseCompose(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        ComposeWire? wire;
        try
        {
            wire = JsonSerializer.Deserialize<ComposeWire>(ExtractJson(text), SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (wire?.Posts is null)
        {
            return null;
        }

        var posts = new List<ComposedPlanPost>();
        foreach (var post in wire.Posts)
        {
            if (string.IsNullOrWhiteSpace(post.Content))
            {
                return null;
            }

            if (!DateTimeOffset.TryParse(post.ScheduledTimeUtc, out var time))
            {
                return null;
            }

            posts.Add(new ComposedPlanPost(post.Content, time.ToUniversalTime()));
        }

        return new ComposedPlan(posts);
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

    private sealed record ScoreWire(List<ScoreAccountWire>? Accounts);
    private sealed record ScoreAccountWire(string? RunItemId, string? Reasoning, int DesireScore, int HypotheticalPostCount);
    private sealed record ComposeWire(List<ComposeItemWire>? Posts);
    private sealed record ComposeItemWire(string? Content, string? ScheduledTimeUtc);
}
