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
        Directory.CreateDirectory(Path.Combine(_root, "10-personalities"));
        Directory.CreateDirectory(Path.Combine(_root, "20-tasks"));
        Directory.CreateDirectory(Path.Combine(_root, "30-priority-instructions"));

        File.WriteAllText(Path.Combine(_root, "00-base.md"), "BASE");
        File.WriteAllText(
            Path.Combine(_root, "10-personalities", "default.md"), "PERSONALITY");
        File.WriteAllText(
            Path.Combine(_root, "20-tasks", "comment-created.md"),
            "actor={{actor_name}} post={{post_content}} thread={{comment_thread}} " +
            "incoming={{incoming_comment}} prio={{priority_instruction}}");
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
        _composer = new PromptComposer(files, new AgentProfileStore(_root));
    }

    [Fact]
    public void Compose_ShouldPutBaseAndPersonalityInSystemPrompt()
    {
        var prompt = _composer.Compose("any-user", SampleInput(EffectivePriority.Normal));

        prompt.System.ShouldBe("BASE\n\nPERSONALITY");
    }

    [Fact]
    public void Compose_ShouldFillAllPlaceholders()
    {
        var prompt = _composer.Compose("any-user", SampleInput(EffectivePriority.Normal));

        prompt.UserMessage.ShouldContain("actor=Alice");
        prompt.UserMessage.ShouldContain("post=My post");
        prompt.UserMessage.ShouldContain("thread=Alice (Human): hi");
        prompt.UserMessage.ShouldContain("incoming=hi");
        prompt.UserMessage.ShouldContain("prio=PRIO-NORMAL");
        prompt.UserMessage.ShouldNotContain("{{");
    }

    [Fact]
    public void Compose_ShouldAlwaysAppendGuardrailsLast()
    {
        var prompt = _composer.Compose("any-user", SampleInput(EffectivePriority.LowClosing));

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
        var prompt = _composer.Compose("any-user", SampleInput(priority));

        prompt.UserMessage.ShouldContain($"prio={expected}");
    }

    private static CommentCreatedPromptInput SampleInput(EffectivePriority priority)
    {
        return new CommentCreatedPromptInput(
            ActorName: "Alice",
            PostContent: "My post",
            CommentThread: "Alice (Human): hi",
            IncomingComment: "hi",
            Priority: priority);
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }
}
