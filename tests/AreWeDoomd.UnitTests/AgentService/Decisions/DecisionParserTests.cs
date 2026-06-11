using AreWeDoomd.AgentService.Decisions;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.AgentService.Decisions;

public sealed class DecisionParserTests
{
    private readonly DecisionParser _parser = new();

    [Fact]
    public void Parse_WhenValidReply_ShouldReturnReplyDecision()
    {
        var text = """{"action":"reply_comment","content":"Nice point!","reasoning":"friendly"}""";

        var decision = _parser.Parse(text);

        decision.ShouldNotBeNull();
        decision!.Action.ShouldBe(AgentAction.ReplyComment);
        decision.Content.ShouldBe("Nice point!");
        decision.Reasoning.ShouldBe("friendly");
    }

    [Fact]
    public void Parse_WhenValidIgnore_ShouldReturnIgnoreDecision()
    {
        var text = """{"action":"ignore","reasoning":"nothing to add"}""";

        var decision = _parser.Parse(text);

        decision.ShouldNotBeNull();
        decision!.Action.ShouldBe(AgentAction.Ignore);
        decision.Content.ShouldBeNull();
    }

    [Fact]
    public void Parse_WhenValidLike_ShouldReturnLikeDecision()
    {
        var text = """{"action":"like_comment","reasoning":"agree"}""";

        var decision = _parser.Parse(text);

        decision.ShouldNotBeNull();
        decision!.Action.ShouldBe(AgentAction.LikeComment);
    }

    [Fact]
    public void Parse_WhenWrappedInMarkdownFences_ShouldStillParse()
    {
        var text = """
        ```json
        {"action":"ignore","reasoning":"fenced"}
        ```
        """;

        var decision = _parser.Parse(text);

        decision.ShouldNotBeNull();
        decision!.Action.ShouldBe(AgentAction.Ignore);
    }

    [Fact]
    public void Parse_WhenUnknownAction_ShouldReturnNull()
    {
        var text = """{"action":"delete_post","reasoning":"nope"}""";

        _parser.Parse(text).ShouldBeNull();
    }

    [Fact]
    public void Parse_WhenReplyWithoutContent_ShouldReturnNull()
    {
        var text = """{"action":"reply_comment","content":"  ","reasoning":"oops"}""";

        _parser.Parse(text).ShouldBeNull();
    }

    [Fact]
    public void Parse_WhenMalformedJson_ShouldReturnNull()
    {
        _parser.Parse("not json at all").ShouldBeNull();
    }

    [Fact]
    public void Parse_WhenNullOrWhitespace_ShouldReturnNull()
    {
        _parser.Parse(null).ShouldBeNull();
        _parser.Parse("   ").ShouldBeNull();
    }
}
