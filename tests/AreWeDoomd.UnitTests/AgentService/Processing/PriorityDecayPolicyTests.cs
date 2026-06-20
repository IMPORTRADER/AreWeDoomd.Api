using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.AgentService;
using AreWeDoomd.AgentService.Context;
using AreWeDoomd.AgentService.Processing;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.AgentService.Processing;

public sealed class PriorityDecayPolicyTests
{
    private readonly PriorityDecayPolicy _policy = new(
        Options.Create(new AgentServiceOptions()));

    [Theory]
    [InlineData(NotificationPriority.Low, EffectivePriority.Low)]
    [InlineData(NotificationPriority.Normal, EffectivePriority.Normal)]
    [InlineData(NotificationPriority.High, EffectivePriority.High)]
    public void Evaluate_WhenActorIsHuman_ShouldPassPriorityThrough(
        NotificationPriority priority,
        EffectivePriority expected)
    {
        var comments = Comments("Ai", "Ai", "Ai", "Ai");

        var result = _policy.Evaluate(ActorType.Human, priority, comments);

        result.ShouldBe(expected);
    }

    [Fact]
    public void Evaluate_WhenAiActorDepth1_ShouldBeNormal()
    {
        var comments = Comments("Human", "Ai");

        var result = _policy.Evaluate(ActorType.Ai, NotificationPriority.Normal, comments);

        result.ShouldBe(EffectivePriority.Normal);
    }

    [Fact]
    public void Evaluate_WhenAiActorDepth2_ShouldBeLow()
    {
        var comments = Comments("Human", "Ai", "Ai");

        var result = _policy.Evaluate(ActorType.Ai, NotificationPriority.Normal, comments);

        result.ShouldBe(EffectivePriority.Low);
    }

    [Fact]
    public void Evaluate_WhenAiActorDepth3_ShouldBeLowClosing()
    {
        var comments = Comments("Human", "Ai", "Ai", "Ai");

        var result = _policy.Evaluate(ActorType.Ai, NotificationPriority.Normal, comments);

        result.ShouldBe(EffectivePriority.LowClosing);
    }

    [Fact]
    public void Evaluate_WhenAiActorDepth4_ShouldBeSkip()
    {
        var comments = Comments("Ai", "Ai", "Ai", "Ai");

        var result = _policy.Evaluate(ActorType.Ai, NotificationPriority.Normal, comments);

        result.ShouldBe(EffectivePriority.Skip);
    }

    [Fact]
    public void Evaluate_WhenAiActorButHumanBrokeTheChain_ShouldResetDepth()
    {
        var comments = Comments("Ai", "Ai", "Human", "Ai");

        var result = _policy.Evaluate(ActorType.Ai, NotificationPriority.Normal, comments);

        result.ShouldBe(EffectivePriority.Normal);
    }

    private static List<CommentInfo> Comments(params string[] authorTypes)
    {
        return authorTypes
            .Select((type, index) => new CommentInfo(
                Guid.NewGuid(),
                $"user{index}",
                type,
                $"comment {index}",
                DateTimeOffset.UtcNow.AddMinutes(index)))
            .ToList();
    }
}
