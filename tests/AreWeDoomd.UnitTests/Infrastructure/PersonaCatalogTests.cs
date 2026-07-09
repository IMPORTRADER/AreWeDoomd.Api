using AreWeDoomd.Infrastructure.Ai;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Infrastructure;

public sealed class PersonaCatalogTests
{
    private readonly PersonaCatalog _catalog = new();

    [Fact]
    public void Get_Always_ReturnsAtLeastTenArchetypes()
    {
        _catalog.Get().Archetypes.Count.ShouldBeGreaterThanOrEqualTo(10);
    }

    [Fact]
    public void Get_EveryArchetype_HasAllRequiredContent()
    {
        foreach (var a in _catalog.Get().Archetypes)
        {
            a.Key.ShouldNotBeNullOrWhiteSpace();
            a.Name.ShouldNotBeNullOrWhiteSpace();
            a.Description.ShouldNotBeNullOrWhiteSpace();
            a.UsernamePatterns.ShouldNotBeEmpty();
            a.Traits.Count.ShouldBeGreaterThanOrEqualTo(3);
            a.TypingStyles.ShouldNotBeEmpty();
            a.Summaries.ShouldNotBeEmpty();
        }
    }

    [Fact]
    public void Get_ArchetypeKeys_AreUnique()
    {
        var keys = _catalog.Get().Archetypes.Select(a => a.Key).ToList();
        keys.Distinct().Count().ShouldBe(keys.Count);
    }

    [Fact]
    public void Get_EveryArchetypeTrait_IsWithinDomainLimits()
    {
        // AiPersonality trait rules: 2–60 chars, max 10 per user
        foreach (var a in _catalog.Get().Archetypes)
        {
            a.Traits.Count.ShouldBeLessThanOrEqualTo(10);
            foreach (var t in a.Traits)
            {
                t.Length.ShouldBeInRange(2, 60);
            }
        }
    }

    [Fact]
    public void Get_WordPools_ContainNounAndAdjective()
    {
        var pools = _catalog.Get().UsernameWordPools;
        pools.ShouldContainKey("noun");
        pools.ShouldContainKey("adjective");
        pools["noun"].ShouldNotBeEmpty();
        pools["adjective"].ShouldNotBeEmpty();
    }

    [Fact]
    public void Get_TraitCategoriesAndTypingSuggestions_AreNonEmpty()
    {
        var data = _catalog.Get();
        data.TraitCategories.ShouldNotBeEmpty();
        data.TraitCategories.ShouldAllBe(c => c.Traits.Count > 0);
        data.TypingStyleSuggestions.Count.ShouldBeGreaterThanOrEqualTo(8);
    }

    [Fact]
    public void Get_EveryUsernamePattern_ResolvesWithinUsernameRules()
    {
        // Longest possible expansion must fit 3–24 chars with only [a-z0-9_].
        var data = _catalog.Get();
        foreach (var pattern in data.Archetypes.SelectMany(a => a.UsernamePatterns))
        {
            var longest = pattern;
            foreach (var (token, pool) in data.UsernameWordPools)
            {
                var longestWord = pool.OrderByDescending(w => w.Length).First();
                longest = longest.Replace("{" + token + "}", longestWord);
            }
            longest = longest.Replace("{nn}", "99");

            longest.Length.ShouldBeInRange(3, 24, $"pattern '{pattern}' expands to '{longest}'");
            longest.ShouldMatch("^[a-z0-9_]+$");
        }
    }
}
