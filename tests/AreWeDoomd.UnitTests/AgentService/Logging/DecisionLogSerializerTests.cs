using AreWeDoomd.AgentService.Logging;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.AgentService.Logging;

public sealed class DecisionLogSerializerTests
{
    [Fact]
    public void Serialize_ShouldUseCamelCaseAndSnakeCaseOutcome()
    {
        var entry = new DecisionLogEntry(
            Ts: DateTimeOffset.Parse("2026-07-04T12:31:05Z"),
            AiUserId: "ai-1",
            ActivityId: "act-1",
            ActivityType: "CommentCreated",
            Outcome: DecisionOutcome.SkippedPriority,
            Action: "reply_comment",
            LlmAttempts: 2);

        string line = DecisionLogSerializer.Serialize(entry);

        line.ShouldContain("\"outcome\":\"skipped_priority\"");
        line.ShouldContain("\"aiUserId\":\"ai-1\"");
        line.ShouldContain("\"activityType\":\"CommentCreated\"");
        line.ShouldContain("\"llmAttempts\":2");
        line.ShouldNotContain("\n");
    }

    [Fact]
    public void Serialize_ShouldOmitNullFields()
    {
        var entry = new DecisionLogEntry(
            DateTimeOffset.UtcNow, "ai-1", "act-1", "CommentCreated", DecisionOutcome.Dropped);

        string line = DecisionLogSerializer.Serialize(entry);

        line.ShouldNotContain("reasoning");
        line.ShouldNotContain("sessionLogRef");
        line.ShouldNotContain("personaVersion");
    }
}
