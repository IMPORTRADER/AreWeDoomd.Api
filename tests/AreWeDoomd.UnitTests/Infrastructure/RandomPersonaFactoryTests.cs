using System.Text.RegularExpressions;
using AreWeDoomd.Infrastructure.Ai;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Infrastructure;

public sealed class RandomPersonaFactoryTests
{
    private static RandomPersonaFactory CreateFactory(int seed) =>
        new(new PersonaCatalog(), new Random(seed));

    [Fact]
    public void CreateRandom_Always_ProducesValidUsername()
    {
        var factory = CreateFactory(seed: 42);

        for (int i = 0; i < 200; i++)
        {
            var persona = factory.CreateRandom();
            persona.Username.Length.ShouldBeInRange(3, 24);
            Regex.IsMatch(persona.Username, "^[a-z0-9_]+$").ShouldBeTrue(
                $"username '{persona.Username}' contains invalid characters");
        }
    }

    [Fact]
    public void CreateRandom_Always_ProducesTraitCountWithinLimits()
    {
        var factory = CreateFactory(seed: 7);

        for (int i = 0; i < 200; i++)
        {
            var persona = factory.CreateRandom();
            persona.Traits.Count.ShouldBeInRange(3, 6);
            persona.Traits.Distinct().Count().ShouldBe(persona.Traits.Count);
        }
    }

    [Fact]
    public void CreateRandom_Always_ProducesNonEmptyTypingStyleAndSummary()
    {
        var factory = CreateFactory(seed: 99);

        for (int i = 0; i < 50; i++)
        {
            var persona = factory.CreateRandom();
            persona.TypingStyle.ShouldNotBeNullOrWhiteSpace();
            persona.Summary.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void CreateRandom_WithSameSeed_IsDeterministic()
    {
        var a = CreateFactory(seed: 1234).CreateRandom();
        var b = CreateFactory(seed: 1234).CreateRandom();

        a.Username.ShouldBe(b.Username);
        a.Traits.ShouldBe(b.Traits);
        a.TypingStyle.ShouldBe(b.TypingStyle);
        a.Summary.ShouldBe(b.Summary);
    }

    [Fact]
    public void CreateRandom_ConsecutiveCalls_ProduceVariedPersonas()
    {
        var factory = CreateFactory(seed: 5);
        var usernames = Enumerable.Range(0, 20)
            .Select(_ => factory.CreateRandom().Username)
            .Distinct()
            .ToList();

        // 20 random draws should not all collapse to a handful of names
        usernames.Count.ShouldBeGreaterThan(10);
    }
}
