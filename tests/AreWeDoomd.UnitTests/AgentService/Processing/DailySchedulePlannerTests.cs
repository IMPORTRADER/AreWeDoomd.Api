using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.AgentService;
using AreWeDoomd.AgentService.Actions;
using AreWeDoomd.AgentService.Decisions;
using AreWeDoomd.AgentService.Processing;
using AreWeDoomd.AgentService.Prompting;
using AreWeDoomd.ChatProviders;
using AreWeDoomd.UnitTests.AgentService;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.AgentService.Processing;

public sealed class DailySchedulePlannerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 6, 10, 0, 0, TimeSpan.Zero);

    private readonly Mock<IChatProvider> _chat = new();
    private readonly Mock<IPersonaProvider> _personas = new();
    private readonly Mock<IScheduleDecisionCallbackClient> _callback = new();
    private readonly List<ScheduleDecisionCallbackClient.CallbackPayload> _submitted = [];
    private readonly FakeAgentOpsLogWriter _opsLog = new();
    private readonly DailySchedulePlanner _planner;

    public DailySchedulePlannerTests()
    {
        _chat.SetupGet(c => c.Name).Returns("test");
        _personas.Setup(p => p.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PersonaResolution(null, PersonaSource.Default));
        _callback
            .Setup(c => c.SubmitAsync(It.IsAny<Guid>(),
                It.IsAny<ScheduleDecisionCallbackClient.CallbackPayload>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, ScheduleDecisionCallbackClient.CallbackPayload, CancellationToken>(
                (_, p, _) => _submitted.Add(p))
            .ReturnsAsync(true);

        var options = Options.Create(new AgentServiceOptions { ChatProvider = "test", ScoringBatchSize = 8 });

        var chatProviderResolver = new Mock<IChatProviderResolver>();
        chatProviderResolver.Setup(r => r.Resolve(It.IsAny<string?>())).Returns(_chat.Object);

        var llmSettings = new Mock<ILlmSettingsProvider>();
        llmSettings.Setup(l => l.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmRuntimeSettings("test-model", "", false, 512, 800, 1024));

        _planner = new DailySchedulePlanner(
            new ScheduleRunQueue(), new PromptFileSet(), new DailyPostPlanParser(),
            _personas.Object, _callback.Object, chatProviderResolver.Object, llmSettings.Object,
            options, new FakeTimeProvider(Now), _opsLog,
            NullLogger<DailySchedulePlanner>.Instance);
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static ScheduleRunRequest Run(params ScheduleRunRequestItem[] items) => new(
        RunId: Guid.NewGuid(), Threshold: 60, MaxPostsPerAccount: 3, PostLengthGuide: 500,
        Strategy: 0, WindowStartUtc: Now.AddMinutes(5), WindowEndUtc: Now.AddHours(8),
        Items: [.. items],
        Model: "openai/gpt-oss-120b:free", ScoringModel: "", ThinkingEnabled: false,
        ScoringTokensPerAccount: 512, CompositionTokensPerPost: 800);

    private static ScheduleRunRequestItem Item(Guid id) =>
        new(id, Guid.NewGuid(), "dogaAi", "meraklı", 0, null);

    [Fact]
    public async Task ProcessRunAsync_ShouldUseSnapshotBudgetAndThinkingForScoring()
    {
        var runItemId = Guid.NewGuid();
        ChatRequest? captured = null;
        _chat.Setup(c => c.CompleteAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ChatRequest, CancellationToken>((r, _) => captured ??= r)
            .ReturnsAsync(ChatResult.Fail(new ChatError("boom", 500, "test")));

        await _planner.ProcessRunAsync(Run(Item(runItemId)), CancellationToken.None);

        captured.ShouldNotBeNull();
        captured!.MaxTokens.ShouldBe(512); // 512 * 1 item
        captured.ReasoningEnabled.ShouldBe(false);
        captured.Model.ShouldBe("openai/gpt-oss-120b:free"); // ScoringModel boş → Model
    }

    [Fact]
    public async Task ProcessRunAsync_WhenScoringFails_ShouldSubmitRealProviderErrorInErrorDetail()
    {
        var runItemId = Guid.NewGuid();
        _chat.Setup(c => c.CompleteAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatResult.Fail(new ChatError(
                "Token budget exhausted before any usable text was produced (thinking=141, output=5).",
                200, "gemini")));

        await _planner.ProcessRunAsync(Run(Item(runItemId)), CancellationToken.None);

        _submitted.Count.ShouldBe(1);
        _submitted[0].RunItemId.ShouldBe(runItemId);
        _submitted[0].ErrorDetail.ShouldNotBeNull();
        _submitted[0].ErrorDetail!.ShouldStartWith("Scoring failed:");
        _submitted[0].ErrorDetail.ShouldContain("Token budget exhausted");
    }

    [Fact]
    public async Task ProcessRunAsync_StagedRun_ShouldWritePerAccountOpsLogEntriesWithAgentIdentity()
    {
        var runItemId = Guid.NewGuid();
        var item = Item(runItemId);
        // Score below threshold (60) so the below-loop per-account log fires without needing composition.
        var scoringJson = $$"""{"accounts":[{"runItemId":"{{runItemId}}","reasoning":"not in the mood","desireScore":20,"hypotheticalPostCount":1}]}""";
        _chat.Setup(c => c.CompleteAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChatResult.Ok(scoringJson, new TokenUsage(10, 10), FinishReason.Stop));

        await _planner.ProcessRunAsync(Run(item), CancellationToken.None);

        var perAccountEntries = _opsLog.Entries
            .Where(e => e.Source == AreWeDoomd.AgentService.Logging.AgentOpsLogSource.Scheduling
                        && e.AiUserId != null && e.AiUsername != null)
            .ToList();

        perAccountEntries.ShouldNotBeEmpty();
        perAccountEntries.ShouldContain(e =>
            e.AiUserId == item.AiUserId.ToString() && e.AiUsername == item.Username);
    }
}
