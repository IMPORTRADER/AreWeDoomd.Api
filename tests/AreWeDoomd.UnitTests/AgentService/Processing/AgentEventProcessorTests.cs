using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.AgentService;
using AreWeDoomd.AgentService.Actions;
using AreWeDoomd.AgentService.Ai;
using AreWeDoomd.AgentService.Context;
using AreWeDoomd.AgentService.Decisions;
using AreWeDoomd.AgentService.Logging;
using AreWeDoomd.AgentService.Processing;
using AreWeDoomd.AgentService.Prompting;
using AreWeDoomd.ChatProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.AgentService.Processing;

public sealed class AgentEventProcessorTests
{
    private static readonly Guid PostId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CommentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const string AiUserId = "33333333-3333-3333-3333-333333333333";

    private readonly Mock<IContextFetcher> _contextFetcher = new();
    private readonly Mock<IPromptComposer> _promptComposer = new();
    private readonly Mock<IChatProvider> _chatProvider = new();
    private readonly Mock<IActionExecutor> _actionExecutor = new();
    private readonly Mock<IAiSessionLogger> _sessionLogger = new();
    private readonly Mock<IDecisionLogWriter> _decisionLog = new();

    public AgentEventProcessorTests()
    {
        _contextFetcher
            .Setup(f => f.FetchAsync(PostId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleContext());
        _promptComposer
            .Setup(c => c.Compose(It.IsAny<string>(), It.IsAny<CommentCreatedPromptInput>()))
            .Returns(new ComposedPrompt("sys", "user"));
        _actionExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<AgentDecision>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActionExecutionResult(ActionExecutionOutcome.Executed));
    }

    [Fact]
    public async Task ProcessSingleAsync_WhenLlmReturnsReply_ShouldExecuteReply()
    {
        SetupLlmResponses("""{"action":"reply_comment","content":"Hi!","reasoning":"r"}""");
        var processor = CreateProcessor();

        await processor.ProcessSingleAsync(SampleEvent(ActorType.Human), CancellationToken.None);

        _actionExecutor.Verify(
            e => e.ExecuteAsync(
                It.Is<AgentDecision>(d => d.Action == AgentAction.ReplyComment && d.Content == "Hi!"),
                PostId,
                CommentId,
                AiUserId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessSingleAsync_WhenFirstLlmOutputInvalid_ShouldRetryOnce()
    {
        SetupLlmResponses(
            "this is not json",
            """{"action":"like_comment","reasoning":"r"}""");
        var processor = CreateProcessor();

        await processor.ProcessSingleAsync(SampleEvent(ActorType.Human), CancellationToken.None);

        _chatProvider.Verify(
            p => p.CompleteAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        _actionExecutor.Verify(
            e => e.ExecuteAsync(
                It.Is<AgentDecision>(d => d.Action == AgentAction.LikeComment),
                PostId,
                CommentId,
                AiUserId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessSingleAsync_WhenBothLlmOutputsInvalid_ShouldLogLlmFallbackAndNotCallExecutor()
    {
        SetupLlmResponses("garbage", "more garbage");
        var processor = CreateProcessor();

        await processor.ProcessSingleAsync(SampleEvent(ActorType.Human), CancellationToken.None);

        _actionExecutor.Verify(
            e => e.ExecuteAsync(
                It.IsAny<AgentDecision>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _decisionLog.Verify(w => w.TryLog(It.Is<DecisionLogEntry>(e =>
            e.Outcome == DecisionOutcome.LlmFallback)), Times.Once);
    }

    [Fact]
    public async Task ProcessSingleAsync_WhenNoAiRecipient_ShouldDoNothing()
    {
        var processor = CreateProcessor();
        var evt = SampleEvent(ActorType.Human) with
        {
            Recipients = new List<AgentEvent.RecipientInfo>()
        };

        await processor.ProcessSingleAsync(evt, CancellationToken.None);

        _contextFetcher.Verify(
            f => f.FetchAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessSingleAsync_WhenAiActorAtSkipDepth_ShouldNotCallLlm()
    {
        // 4 consecutive AI-authored comments at the tail → Skip.
        _contextFetcher
            .Setup(f => f.FetchAsync(PostId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleContext("Ai", "Ai", "Ai", "Ai"));
        var processor = CreateProcessor();

        await processor.ProcessSingleAsync(SampleEvent(ActorType.Ai), CancellationToken.None);

        _chatProvider.Verify(
            p => p.CompleteAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _actionExecutor.Verify(
            e => e.ExecuteAsync(
                It.IsAny<AgentDecision>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessSingleAsync_WhenContextFetchFails_ShouldDropEvent()
    {
        _contextFetcher
            .Setup(f => f.FetchAsync(PostId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PostContext?)null);
        var processor = CreateProcessor();

        await processor.ProcessSingleAsync(SampleEvent(ActorType.Human), CancellationToken.None);

        _chatProvider.Verify(
            p => p.CompleteAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── New decision-log tests ──────────────────────────────────────────────

    [Fact]
    public async Task ProcessSingleAsync_WhenReplyExecuted_ShouldLogExecutedOutcomeOnce()
    {
        SetupLlmResponses("""{"action":"reply_comment","content":"Hi!","reasoning":"r"}""");
        _actionExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<AgentDecision>(), PostId, CommentId, AiUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActionExecutionResult(ActionExecutionOutcome.Executed));
        var processor = CreateProcessor();

        await processor.ProcessSingleAsync(SampleEvent(ActorType.Human), CancellationToken.None);

        _decisionLog.Verify(w => w.TryLog(It.Is<DecisionLogEntry>(e =>
            e.Outcome == DecisionOutcome.Executed &&
            e.Action == "reply_comment" &&
            e.Reasoning == "r" &&
            e.AiUserId == AiUserId &&
            e.PostId == PostId)), Times.Once);
        _decisionLog.Verify(w => w.TryLog(It.IsAny<DecisionLogEntry>()), Times.Once);
    }

    [Fact]
    public async Task ProcessSingleAsync_WhenProviderFailsTwice_ShouldLogLlmFailed()
    {
        SetupLlmFailures("provider down", "provider down");
        var processor = CreateProcessor();

        await processor.ProcessSingleAsync(SampleEvent(ActorType.Human), CancellationToken.None);

        _decisionLog.Verify(w => w.TryLog(It.Is<DecisionLogEntry>(e =>
            e.Outcome == DecisionOutcome.LlmFailed && e.LlmAttempts == 2)), Times.Once);
        _actionExecutor.Verify(
            e => e.ExecuteAsync(It.IsAny<AgentDecision>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessSingleAsync_WhenLlmIgnores_ShouldLogIgnoredOutcome()
    {
        SetupLlmResponses("""{"action":"ignore","reasoning":"not my thread"}""");
        _actionExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<AgentDecision>(), PostId, CommentId, AiUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActionExecutionResult(ActionExecutionOutcome.Ignored));
        var processor = CreateProcessor();

        await processor.ProcessSingleAsync(SampleEvent(ActorType.Human), CancellationToken.None);

        _decisionLog.Verify(w => w.TryLog(It.Is<DecisionLogEntry>(e =>
            e.Outcome == DecisionOutcome.Ignored && e.Reasoning == "not my thread")), Times.Once);
    }

    [Fact]
    public async Task ProcessSingleAsync_WhenActionFails_ShouldLogActionFailedWithDetail()
    {
        SetupLlmResponses("""{"action":"reply_comment","content":"Hi!","reasoning":"r"}""");
        _actionExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<AgentDecision>(), PostId, CommentId, AiUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActionExecutionResult(ActionExecutionOutcome.Failed, "HTTP 500: boom"));
        var processor = CreateProcessor();

        await processor.ProcessSingleAsync(SampleEvent(ActorType.Human), CancellationToken.None);

        _decisionLog.Verify(w => w.TryLog(It.Is<DecisionLogEntry>(e =>
            e.Outcome == DecisionOutcome.ActionFailed && e.ErrorDetail == "HTTP 500: boom")), Times.Once);
    }

    [Fact]
    public async Task ProcessSingleAsync_WhenBothLlmOutputsInvalidJson_ShouldLogLlmFallback()
    {
        SetupLlmResponses("not json", "not json");
        var processor = CreateProcessor();

        await processor.ProcessSingleAsync(SampleEvent(ActorType.Human), CancellationToken.None);

        _decisionLog.Verify(w => w.TryLog(It.Is<DecisionLogEntry>(e =>
            e.Outcome == DecisionOutcome.LlmFallback && e.LlmAttempts == 2)), Times.Once);
        _actionExecutor.Verify(
            e => e.ExecuteAsync(It.IsAny<AgentDecision>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessSingleAsync_WhenAiActorAtSkipDepth_ShouldLogSkippedPriority()
    {
        _contextFetcher
            .Setup(f => f.FetchAsync(PostId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleContext("Ai", "Ai", "Ai", "Ai"));
        var processor = CreateProcessor();

        await processor.ProcessSingleAsync(SampleEvent(ActorType.Ai), CancellationToken.None);

        _decisionLog.Verify(w => w.TryLog(It.Is<DecisionLogEntry>(e =>
            e.Outcome == DecisionOutcome.SkippedPriority &&
            e.Priority == "Skip" &&
            e.AiUserId == AiUserId)), Times.Once);
        _decisionLog.Verify(w => w.TryLog(It.IsAny<DecisionLogEntry>()), Times.Once);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void SetupLlmResponses(params string[] texts)
    {
        var sequence = _chatProvider.SetupSequence(
            p => p.CompleteAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()));
        foreach (string text in texts)
        {
            sequence = sequence.ReturnsAsync(
                ChatResult.Ok(text, new TokenUsage(1, 1), FinishReason.Stop));
        }
    }

    private void SetupLlmFailures(params string[] errorMessages)
    {
        var sequence = _chatProvider.SetupSequence(
            p => p.CompleteAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()));
        foreach (string message in errorMessages)
        {
            sequence = sequence.ReturnsAsync(
                ChatResult.Fail(new ChatError(message, null, "test")));
        }
    }

    private AgentEventProcessor CreateProcessor()
    {
        var options = Options.Create(new AgentServiceOptions { Model = "test-model" });
        return new AgentEventProcessor(
            new AgentEventQueue(),
            _contextFetcher.Object,
            new PriorityDecayPolicy(options),
            _promptComposer.Object,
            _chatProvider.Object,
            new DecisionParser(),
            _actionExecutor.Object,
            _sessionLogger.Object,
            _decisionLog.Object,
            options,
            NullLogger<AgentEventProcessor>.Instance);
    }

    private static PostContext SampleContext(params string[] extraAiTailTypes)
    {
        var comments = new List<CommentInfo>
        {
            new(Guid.NewGuid(), "alice", "Human", "First!", DateTimeOffset.UtcNow.AddMinutes(-10))
        };

        foreach (string type in extraAiTailTypes)
        {
            comments.Add(new CommentInfo(
                Guid.NewGuid(), "otherbot", type, "beep", DateTimeOffset.UtcNow.AddMinutes(-5)));
        }

        comments.Add(new CommentInfo(
            CommentId,
            extraAiTailTypes.Length > 0 ? "otherbot" : "alice",
            extraAiTailTypes.Length > 0 ? "Ai" : "Human",
            "What do you think?",
            DateTimeOffset.UtcNow));

        return new PostContext(
            new PostInfo(PostId, "doombot", "Ai", "Is AGI near?"),
            comments);
    }

    private static AgentEvent SampleEvent(ActorType actorType)
    {
        return new AgentEvent(
            ActivityId: "act_test",
            ActivityType: ActivityType.CommentCreated,
            OccurredAt: DateTimeOffset.UtcNow,
            Actor: new AgentEvent.ActorInfo("actor-id", actorType, "Alice"),
            Content: new AgentEvent.ContentInfo(
                CommentId.ToString(), ActivityObjectType.Comment, "What do you think?"),
            Target: new AgentEvent.TargetInfo(PostId.ToString(), ActivityTargetType.Post),
            Recipients: new List<AgentEvent.RecipientInfo>
            {
                new(
                    AiUserId,
                    NotificationRecipientType.Ai,
                    NotificationReason.PostOwner,
                    "post.comment.created",
                    new Dictionary<string, string>(),
                    "dedupe",
                    NotificationPriority.Normal)
            });
    }
}
