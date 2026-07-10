using AreWeDoomd.Application.Notifications.Engine;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.Notifications;

public sealed class MentionParserTests
{
    [Fact]
    public void Extract_WhenContentHasSingleMention_ShouldReturnUsername()
    {
        var result = MentionParser.Extract("hey @selin what do you think?");

        result.ShouldBe(new[] { "selin" });
    }

    [Fact]
    public void Extract_WhenMentionRepeatsWithDifferentCasing_ShouldReturnSingleEntry()
    {
        var result = MentionParser.Extract("@Selin and again @selin");

        result.Count.ShouldBe(1);
    }

    [Fact]
    public void Extract_WhenTokenIsInsideEmailAddress_ShouldIgnoreIt()
    {
        var result = MentionParser.Extract("mail me at doga@example.com");

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Extract_WhenTokenIsTooShortOrTooLong_ShouldIgnoreIt()
    {
        var tooLong = "@" + new string('a', 25);

        MentionParser.Extract("hi @ab").ShouldBeEmpty();
        MentionParser.Extract(tooLong).ShouldBeEmpty();
    }

    [Fact]
    public void Extract_WhenContentIsNullOrWhitespace_ShouldReturnEmpty()
    {
        MentionParser.Extract(null).ShouldBeEmpty();
        MentionParser.Extract("   ").ShouldBeEmpty();
    }

    [Fact]
    public void Extract_WhenMentionAtStartOrAfterPunctuation_ShouldMatch()
    {
        MentionParser.Extract("@selin hi").ShouldBe(new[] { "selin" });
        MentionParser.Extract("(cc @selin, @kaan)").ShouldBe(new[] { "selin", "kaan" });
    }

    [Fact]
    public void Extract_WhenAtSignIsDoubled_ShouldIgnoreIt()
    {
        MentionParser.Extract("weird @@selin token").ShouldBeEmpty();
    }
}
