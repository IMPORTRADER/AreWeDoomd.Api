using System.Text.RegularExpressions;
using AreWeDoomd.Application.Common.Interfaces;
using AreWeDoomd.Application.Common.Models;

namespace AreWeDoomd.Infrastructure.Ai;

/// <summary>
/// Picks a random archetype from the catalog and derives a persona from it:
/// username from a pattern + word pools, a shuffled trait subset, and a
/// random pick among the archetype's typing styles and summaries — so bulk
/// users sharing an archetype are not identical clones.
/// </summary>
public sealed class RandomPersonaFactory : IPersonaFactory
{
    private const int UsernameMaxLength = 24;
    private const int MinTraits = 3;
    private const int MaxTraits = 6;

    private static readonly Regex TokenPattern = new(@"\{(\w+)\}", RegexOptions.Compiled);

    private readonly IPersonaCatalog _catalog;
    private readonly Random _random;

    public RandomPersonaFactory(IPersonaCatalog catalog, Random random)
    {
        _catalog = catalog;
        _random = random;
    }

    public GeneratedPersona CreateRandom()
    {
        var data = _catalog.Get();
        var archetype = data.Archetypes[_random.Next(data.Archetypes.Count)];

        var pattern = archetype.UsernamePatterns[_random.Next(archetype.UsernamePatterns.Count)];
        var username = ResolvePattern(pattern, data.UsernameWordPools);

        var traits = PickTraits(archetype.Traits);
        var typingStyle = archetype.TypingStyles[_random.Next(archetype.TypingStyles.Count)];
        var summary = archetype.Summaries[_random.Next(archetype.Summaries.Count)];

        return new GeneratedPersona(username, traits, typingStyle, summary);
    }

    private string ResolvePattern(
        string pattern, IReadOnlyDictionary<string, IReadOnlyList<string>> pools)
    {
        var resolved = TokenPattern.Replace(pattern, match =>
        {
            var token = match.Groups[1].Value;
            if (token == "nn")
            {
                return _random.Next(10, 100).ToString();
            }

            if (pools.TryGetValue(token, out var pool) && pool.Count > 0)
            {
                return pool[_random.Next(pool.Count)];
            }

            return string.Empty;
        });

        return resolved.Length <= UsernameMaxLength ? resolved : resolved[..UsernameMaxLength];
    }

    private IReadOnlyList<string> PickTraits(IReadOnlyList<string> traits)
    {
        var shuffled = traits.ToList();
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        int count = Math.Min(shuffled.Count, _random.Next(MinTraits, MaxTraits + 1));
        return shuffled.Take(count).ToList();
    }
}
