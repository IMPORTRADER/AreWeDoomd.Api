using AreWeDoomd.AgentService.Decisions;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.AgentService.Decisions;

public sealed class DecisionParserTests
{
    private readonly DecisionParser _parser = new();

    [Fact]
    public void Parse_WhenActionsArrayIsEmpty_ShouldReturnEmptyDecision()
    {
        var decision = _parser.Parse("""{"actions":[],"reasoning":"nothing to add"}""");

        decision.ShouldNotBeNull();
        decision!.Actions.ShouldBeEmpty();
        decision.Reasoning.ShouldBe("nothing to add");
    }

    [Fact]
    public void Parse_WhenActionsAreValid_ShouldPreserveTheirOrder()
    {
        var decision = _parser.Parse("""
            {"actions":[{"type":"like_post"},{"type":"like_comment"},{"type":"reply_comment","content":"Nice point!"}],"reasoning":"friendly"}
            """);

        decision.ShouldNotBeNull();
        decision!.Actions.ShouldBe([
            new AgentActionDecision(AgentAction.LikePost, null),
            new AgentActionDecision(AgentAction.LikeComment, null),
            new AgentActionDecision(AgentAction.ReplyComment, "Nice point!")
        ]);
        decision.Reasoning.ShouldBe("friendly");
    }

    [Fact]
    public void Parse_WhenReplyHasNoContent_ShouldDropOnlyReply()
    {
        var decision = _parser.Parse("""
            {"actions":[{"type":"like_post"},{"type":"reply_comment","content":"  "},{"type":"like_comment"}],"reasoning":"agree"}
            """);

        decision.ShouldNotBeNull();
        decision!.Actions.ShouldBe([
            new AgentActionDecision(AgentAction.LikePost, null),
            new AgentActionDecision(AgentAction.LikeComment, null)
        ]);
    }

    [Fact]
    public void Parse_WhenActionTypeIsDuplicated_ShouldRetainItOnlyOnce()
    {
        var decision = _parser.Parse("""
            {"actions":[{"type":"like_post"},{"type":"like_post"},{"type":"like_comment"}],"reasoning":"agree"}
            """);

        decision.ShouldNotBeNull();
        decision!.Actions.ShouldBe([
            new AgentActionDecision(AgentAction.LikePost, null),
            new AgentActionDecision(AgentAction.LikeComment, null)
        ]);
    }

    [Fact]
    public void Parse_WhenMoreThanThreeActionsAreValid_ShouldLimitResultsToThree()
    {
        var decision = _parser.Parse("""
            {"actions":[{"type":"like_post"},{"type":"like_comment"},{"type":"reply_comment","content":"One"},{"type":"reply_comment","content":"Two"}],"reasoning":"engaged"}
            """);

        decision.ShouldNotBeNull();
        decision!.Actions.ShouldBe([
            new AgentActionDecision(AgentAction.LikePost, null),
            new AgentActionDecision(AgentAction.LikeComment, null),
            new AgentActionDecision(AgentAction.ReplyComment, "One")
        ]);
    }

    [Fact]
    public void Parse_WhenOnlyUnknownActionsAreSupplied_ShouldReturnEmptyDecision()
    {
        var decision = _parser.Parse("""{"actions":[{"type":"delete_post"}],"reasoning":"nope"}""");

        decision.ShouldNotBeNull();
        decision!.Actions.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_WhenWrappedInMarkdownFences_ShouldParseActions()
    {
        var decision = _parser.Parse("""
            ```json
            {"actions":[{"type":"like_post"}],"reasoning":"fenced"}
            ```
            """);

        decision.ShouldNotBeNull();
        decision!.Actions.ShouldBe([new AgentActionDecision(AgentAction.LikePost, null)]);
    }

    [Fact]
    public void Parse_WhenJsonIsMalformed_ShouldReturnNull()
    {
        _parser.Parse("not json at all").ShouldBeNull();
    }

    [Fact]
    public void Parse_WhenActionsArrayIsMissing_ShouldReturnNull()
    {
        _parser.Parse("""{"reasoning":"missing actions"}""").ShouldBeNull();
    }
}
