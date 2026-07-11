using AreWeDoomd.AgentService.Processing;
using AreWeDoomd.AgentService.Prompting;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.AgentService.Prompting;

public sealed class PromptComposerTests : IDisposable
{
    private readonly string _root;
    private readonly PromptComposer _composer;

    public PromptComposerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"prompts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_root, "20-tasks"));
        Directory.CreateDirectory(Path.Combine(_root, "30-priority-instructions"));

        File.WriteAllText(Path.Combine(_root, "00-base.md"), "BASE");
        File.WriteAllText(
            Path.Combine(_root, "20-tasks", "comment-created.md"),
            "actor={{actor_name}} post={{post_content}} comments={{comments}} " +
            "incoming={{incoming_comment}} mention={{mention_note}} prio={{priority_instruction}}");
        File.WriteAllText(
            Path.Combine(_root, "20-tasks", "post-mentioned.md"),
            "actor={{actor_name}} post={{post_content}} comments={{comments}} " +
            "prio={{priority_instruction}}");
        File.WriteAllText(Path.Combine(_root, "20-tasks", "daily-post-score.md"), "DAILY-POST-SCORE");
        File.WriteAllText(Path.Combine(_root, "20-tasks", "daily-post-compose.md"), "DAILY-POST-COMPOSE");
        File.WriteAllText(
            Path.Combine(_root, "30-priority-instructions", "high.md"), "PRIO-HIGH");
        File.WriteAllText(
            Path.Combine(_root, "30-priority-instructions", "normal.md"), "PRIO-NORMAL");
        File.WriteAllText(
            Path.Combine(_root, "30-priority-instructions", "low.md"), "PRIO-LOW");
        File.WriteAllText(
            Path.Combine(_root, "30-priority-instructions", "closing.md"), "PRIO-CLOSING");
        File.WriteAllText(Path.Combine(_root, "90-guardrails.md"), "GUARDRAILS");

        var files = new PromptFileSet(_root);
        _composer = new PromptComposer(files);
    }

    [Fact]
    public void Compose_WhenPersonaIsNull_ShouldUseDefaultPersonalityInSystemPrompt()
    {
        var prompt = _composer.Compose(null, SampleInput(EffectivePriority.Normal));

        prompt.System.ShouldBe("BASE\n\n" + PersonaPromptRenderer.DefaultPersonality);
    }

    [Fact]
    public void Compose_WhenPersonaProvided_ShouldRenderPersonaInSystemPrompt()
    {
        var persona = new AgentPersona(["Curious", "Witty"], "Short sentences.", "I am a test bot.", 1);

        var prompt = _composer.Compose(persona, SampleInput(EffectivePriority.Normal));

        prompt.System.ShouldStartWith("BASE\n\n");
        prompt.System.ShouldContain("I am a test bot.");
        prompt.System.ShouldContain("Curious");
        prompt.System.ShouldContain("Short sentences.");
    }

    [Fact]
    public void Compose_ShouldFillAllPlaceholders()
    {
        var prompt = _composer.Compose(null, SampleInput(EffectivePriority.Normal));

        prompt.UserMessage.ShouldContain("actor=Alice");
        prompt.UserMessage.ShouldContain("post=My post");
        prompt.UserMessage.ShouldContain("comments=Alice (Human): hi");
        prompt.UserMessage.ShouldContain("incoming=hi");
        prompt.UserMessage.ShouldContain("prio=PRIO-NORMAL");
        prompt.UserMessage.ShouldNotContain("{{");
    }

    [Fact]
    public void Compose_ShouldAlwaysAppendGuardrailsLast()
    {
        var prompt = _composer.Compose(null, SampleInput(EffectivePriority.LowClosing));

        prompt.UserMessage.ShouldEndWith("GUARDRAILS");
    }

    [Theory]
    [InlineData(EffectivePriority.High, "PRIO-HIGH")]
    [InlineData(EffectivePriority.Low, "PRIO-LOW")]
    [InlineData(EffectivePriority.LowClosing, "PRIO-CLOSING")]
    public void Compose_ShouldSelectPriorityInstructionByEffectivePriority(
        EffectivePriority priority,
        string expected)
    {
        var prompt = _composer.Compose(null, SampleInput(priority));

        prompt.UserMessage.ShouldContain($"prio={expected}");
    }

    [Fact]
    public void Compose_WhenIsMentionedTrue_ShouldRenderMentionNoteIntoTask()
    {
        var prompt = _composer.Compose(null, SampleInput(EffectivePriority.Normal, isMentioned: true));

        prompt.UserMessage.ShouldContain(
            "mention=Note: Alice mentioned you directly with @your_username in this comment " +
            "— they are addressing you and most likely expect an answer from you.");
        prompt.UserMessage.ShouldNotContain("{{mention_note}}");
    }

    [Fact]
    public void Compose_WhenIsMentionedFalse_ShouldRemoveMentionNotePlaceholder()
    {
        var prompt = _composer.Compose(null, SampleInput(EffectivePriority.Normal, isMentioned: false));

        prompt.UserMessage.ShouldContain("mention= prio=");
        prompt.UserMessage.ShouldNotContain("{{mention_note}}");
        prompt.UserMessage.ShouldNotContain("mentioned you directly");
    }

    [Fact]
    public void Compose_PostMentioned_ShouldFillAllPlaceholders()
    {
        var prompt = _composer.Compose(null, SamplePostMentionedInput(EffectivePriority.Normal));

        prompt.UserMessage.ShouldContain("actor=Alice");
        prompt.UserMessage.ShouldContain("post=My post");
        prompt.UserMessage.ShouldContain("comments=Alice (Human): hi");
        prompt.UserMessage.ShouldContain("prio=PRIO-NORMAL");
        prompt.UserMessage.ShouldNotContain("{{");
    }

    private static CommentCreatedPromptInput SampleInput(EffectivePriority priority, bool isMentioned = false)
    {
        return new CommentCreatedPromptInput(
            ActorName: "Alice",
            PostContent: "My post",
            Comments: "Alice (Human): hi",
            IncomingComment: "hi",
            Priority: priority,
            IsMentioned: isMentioned);
    }

    private static PostMentionedPromptInput SamplePostMentionedInput(EffectivePriority priority)
    {
        return new PostMentionedPromptInput(
            ActorName: "Alice",
            PostContent: "My post",
            Comments: "Alice (Human): hi",
            Priority: priority);
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }
}
