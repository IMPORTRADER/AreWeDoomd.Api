using System.Text.Json;

namespace AreWeDoomd.Domain.Users;

public sealed class AiPersonality
{
    public const int MinTraits = 1;
    public const int MaxTraits = 10;
    public const int MinTraitLength = 2;
    public const int MaxTraitLength = 60;
    public const int MaxTypingStyleLength = 500;
    public const int MaxSummaryLength = 1000;

    private IReadOnlyList<string>? _traitsCache;

    private AiPersonality()
    {
        TraitsJson = "[]";
        TypingStyle = string.Empty;
        Summary = string.Empty;
    }

    private AiPersonality(
        string traitsJson,
        string typingStyle,
        string summary,
        int version,
        string? schedulePrefsJson,
        string? trendPrefsJson,
        DateTimeOffset updatedAt)
    {
        TraitsJson = traitsJson;
        TypingStyle = typingStyle;
        Summary = summary;
        Version = version;
        SchedulePrefsJson = schedulePrefsJson;
        TrendPrefsJson = trendPrefsJson;
        UpdatedAt = updatedAt;
    }

    public string TraitsJson { get; }

    public IReadOnlyList<string> Traits =>
        _traitsCache ??= JsonSerializer.Deserialize<List<string>>(TraitsJson) ?? [];

    public string TypingStyle { get; }

    public string Summary { get; }

    public int Version { get; }

    public string? SchedulePrefsJson { get; }

    public string? TrendPrefsJson { get; }

    public DateTimeOffset UpdatedAt { get; }

    public static AiPersonality Create(
        IReadOnlyList<string> traits,
        string typingStyle,
        string summary,
        int version,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(traits);

        if (traits.Count is < MinTraits or > MaxTraits)
        {
            throw new ArgumentOutOfRangeException(nameof(traits), traits.Count,
                $"An AI personality needs between {MinTraits} and {MaxTraits} traits.");
        }

        var cleaned = new List<string>(traits.Count);
        foreach (string raw in traits)
        {
            string trait = raw?.Trim() ?? string.Empty;
            if (trait.Length < MinTraitLength)
            {
                throw new ArgumentException(
                    $"Trait '{raw}' is shorter than {MinTraitLength} characters.", nameof(traits));
            }

            if (trait.Length > MaxTraitLength)
            {
                throw new ArgumentOutOfRangeException(nameof(traits), trait.Length,
                    $"Trait '{trait}' exceeds {MaxTraitLength} characters.");
            }

            cleaned.Add(trait);
        }

        if (string.IsNullOrWhiteSpace(typingStyle))
        {
            throw new ArgumentException("Typing style is required.", nameof(typingStyle));
        }

        if (typingStyle.Length > MaxTypingStyleLength)
        {
            throw new ArgumentOutOfRangeException(nameof(typingStyle), typingStyle.Length,
                $"Typing style exceeds {MaxTypingStyleLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Summary is required.", nameof(summary));
        }

        if (summary.Length > MaxSummaryLength)
        {
            throw new ArgumentOutOfRangeException(nameof(summary), summary.Length,
                $"Summary exceeds {MaxSummaryLength} characters.");
        }

        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "Version starts at 1.");
        }

        return new AiPersonality(
            JsonSerializer.Serialize(cleaned),
            typingStyle,
            summary,
            version,
            schedulePrefsJson: null,
            trendPrefsJson: null,
            now);
    }
}
