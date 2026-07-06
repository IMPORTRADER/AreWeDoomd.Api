using AreWeDoomd.Domain.Users;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Domain.Users;

public sealed class AiPersonalityTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-04T12:00:00Z");
    private static readonly string[] ValidTraits = ["toxic", "ai-rights-activist"];
    private const string ValidHash = "this-is-a-valid-hash-string";

    private static User AiUser() => User.Create("botty", "botty@ai.test", ValidHash, UserType.Ai, Now);
    private static User HumanUser() => User.Create("doga", "doga@test.com", ValidHash, UserType.Human, Now);

    [Fact]
    public void Create_WithValidInput_ShouldExposeTraitsAndRoundTripJson()
    {
        var p = AiPersonality.Create(ValidTraits, "gen-z slang, lots of emojis", "A chaotic doomer.", 1, Now);

        p.Traits.ShouldBe(ValidTraits);
        p.TraitsJson.ShouldContain("\"toxic\"");
        p.TypingStyle.ShouldBe("gen-z slang, lots of emojis");
        p.Summary.ShouldBe("A chaotic doomer.");
        p.Version.ShouldBe(1);
        p.SchedulePrefsJson.ShouldBeNull();
        p.TrendPrefsJson.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Create_WithBadTraitCount_ShouldThrow(int count)
    {
        var traits = Enumerable.Range(0, count).Select(i => $"trait-{i}").ToList();
        Action act = () => AiPersonality.Create(traits, "style", "summary", 1, Now);
        act.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("x")]   // < 2 chars
    [InlineData("")]    // empty
    public void Create_WithBadTraitLength_ShouldThrow(string trait)
    {
        Action act = () => AiPersonality.Create([trait], "style", "summary", 1, Now);
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Create_WithTraitOver60Chars_ShouldThrow()
    {
        Action act = () => AiPersonality.Create([new string('x', 61)], "style", "summary", 1, Now);
        act.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_WithTypingStyleOver500_ShouldThrow()
    {
        Action act = () => AiPersonality.Create(ValidTraits, new string('x', 501), "summary", 1, Now);
        act.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_WithSummaryOver1000_ShouldThrow()
    {
        Action act = () => AiPersonality.Create(ValidTraits, "style", new string('x', 1001), 1, Now);
        act.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SetAiPersonality_OnAiUser_ShouldSetAndBumpVersionOnReplace()
    {
        var user = AiUser();

        user.SetAiPersonality(ValidTraits, "style-1", "summary-1", Now);
        user.AiPersonality.ShouldNotBeNull();
        user.AiPersonality.Version.ShouldBe(1);

        user.SetAiPersonality(ValidTraits, "style-2", "summary-2", Now.AddMinutes(1));
        user.AiPersonality.Version.ShouldBe(2);
        user.AiPersonality.TypingStyle.ShouldBe("style-2");
    }

    [Fact]
    public void SetAiPersonality_OnHumanUser_ShouldThrow()
    {
        var user = HumanUser();
        Action act = () => user.SetAiPersonality(ValidTraits, "style", "summary", Now);
        act.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void User_WhenCreated_ShouldHaveNoPersonalityAndNotBeAdmin()
    {
        var user = AiUser();
        user.AiPersonality.ShouldBeNull();
        user.IsAdmin.ShouldBeFalse();
    }
}
