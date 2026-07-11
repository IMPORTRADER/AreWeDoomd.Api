using System.Diagnostics;
using System.Text.Json;
using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.AgentService.Actions;
using AreWeDoomd.AgentService.Ai;
using AreWeDoomd.AgentService.Context;
using AreWeDoomd.AgentService.Decisions;
using AreWeDoomd.AgentService.Logging;
using AreWeDoomd.AgentService.Prompting;
using AreWeDoomd.ChatProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.AgentService.Processing;

public sealed class AgentEventProcessor : BackgroundService
{
    private readonly AgentEventQueue _queue;
    private readonly IContextFetcher _contextFetcher;
    private readonly PriorityDecayPolicy _decayPolicy;
    private readonly IPersonaProvider _personaProvider;
    private readonly IPromptComposer _promptComposer;
    private readonly IChatProviderResolver _chatProviderResolver;
    private readonly DecisionParser _decisionParser;
    private readonly IActionExecutor _actionExecutor;
    private readonly IAiSessionLogger _sessionLogger;
    private readonly IDecisionLogWriter _decisionLog;
    private readonly IAgentOpsLogWriter _opsLog;
    private readonly ILlmSettingsProvider _llmSettings;
    private readonly AgentServiceOptions _options;
    private readonly ILogger<AgentEventProcessor> _logger;

    public AgentEventProcessor(
        AgentEventQueue queue,
        IContextFetcher contextFetcher,
        PriorityDecayPolicy decayPolicy,
        IPersonaProvider personaProvider,
        IPromptComposer promptComposer,
        IChatProviderResolver chatProviderResolver,
        DecisionParser decisionParser,
        IActionExecutor actionExecutor,
        IAiSessionLogger sessionLogger,
        IDecisionLogWriter decisionLog,
        IAgentOpsLogWriter opsLog,
        ILlmSettingsProvider llmSettings,
        IOptions<AgentServiceOptions> options,
        ILogger<AgentEventProcessor> logger)
    {
        _queue = queue;
        _contextFetcher = contextFetcher;
        _decayPolicy = decayPolicy;
        _personaProvider = personaProvider;
        _promptComposer = promptComposer;
        _chatProviderResolver = chatProviderResolver;
        _decisionParser = decisionParser;
        _actionExecutor = actionExecutor;
        _sessionLogger = sessionLogger;
        _decisionLog = decisionLog;
        _opsLog = opsLog;
        _llmSettings = llmSettings;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            AgentEvent agentEvent;
            try
            {
                agentEvent = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await ProcessSingleAsync(agentEvent, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unhandled failure processing agent event {ActivityId}",
                    agentEvent.ActivityId);
            }
        }
    }

    public async Task ProcessSingleAsync(AgentEvent agentEvent, CancellationToken ct)
    {
        var aiRecipient = agentEvent.Recipients
            .FirstOrDefault(r => r.RecipientType == NotificationRecipientType.Ai);
        if (aiRecipient is null)
        {
            return;
        }

        // Dispatch on activity type; CommentCreated and PostCreated (mention) each have
        // an agent pipeline today. New scheduled-post event types will slot in as
        // additional cases without surgery to the existing pipelines.
        switch (agentEvent.ActivityType)
        {
            case ActivityType.CommentCreated:
                await ProcessCommentCreatedAsync(agentEvent, aiRecipient, ct);
                break;
            case ActivityType.PostCreated:
                await ProcessPostMentionedAsync(agentEvent, aiRecipient, ct);
                break;
            default:
                _logger.LogDebug(
                    "No agent pipeline for activity type {ActivityType}; skipping {ActivityId}.",
                    agentEvent.ActivityType,
                    agentEvent.ActivityId);
                break;
        }
    }

    private async Task ProcessCommentCreatedAsync(
        AgentEvent agentEvent,
        AgentEvent.RecipientInfo aiRecipient,
        CancellationToken ct)
    {
        if (!Guid.TryParse(agentEvent.Target.Id, out var postId) ||
            !Guid.TryParse(agentEvent.Content.Id, out var commentId))
        {
            _logger.LogWarning(
                "Agent event {ActivityId} carries unparseable ids; dropping.",
                agentEvent.ActivityId);
            return;
        }

        var context = await _contextFetcher.FetchAsync(postId, aiRecipient.UserId, ct);
        if (context is null)
        {
            _logger.LogWarning(
                "Context fetch failed for post {PostId}; dropping event {ActivityId}.",
                postId,
                agentEvent.ActivityId);
            _opsLog.TryLog(new AgentOpsLogEntry(
                DateTimeOffset.UtcNow, AgentOpsLogLevel.Warning, AgentOpsLogSource.Pipeline,
                $"Context fetch failed for post {postId}; event dropped.",
                AiUserId: aiRecipient.UserId, ActivityId: agentEvent.ActivityId));
            return;
        }

        var priority = _decayPolicy.Evaluate(agentEvent.Actor.Type, aiRecipient.Priority, context.Comments);
        if (priority == EffectivePriority.Skip)
        {
            _logger.LogInformation(
                "AI↔AI conversation decayed out at event {ActivityId}; not calling the LLM.",
                agentEvent.ActivityId);
            _opsLog.TryLog(new AgentOpsLogEntry(
                DateTimeOffset.UtcNow, AgentOpsLogLevel.Info, AgentOpsLogSource.Pipeline,
                "AI↔AI conversation decayed out; LLM not called.",
                AiUserId: aiRecipient.UserId, ActivityId: agentEvent.ActivityId));
            // skipped_priority is written BEFORE persona resolution; PersonaVersion/PersonaSource are null here by design.
            _decisionLog.TryLog(new DecisionLogEntry(
                Ts: DateTimeOffset.UtcNow,
                AiUserId: aiRecipient.UserId,
                ActivityId: agentEvent.ActivityId,
                ActivityType: agentEvent.ActivityType.ToString(),
                Outcome: DecisionOutcome.SkippedPriority,
                PostId: postId,
                CommentId: commentId,
                Priority: priority.ToString()));
            return;
        }

        // Resolve persona for the acting AI user. Parse the UserId defensively; if
        // it is not a valid Guid, fall back to no-persona rather than throwing.
        var personaResolution = Guid.TryParse(aiRecipient.UserId, out var parsedAiUserId)
            ? await _personaProvider.GetAsync(parsedAiUserId, ct)
            : new PersonaResolution(null, PersonaSource.Default);

        string incomingComment = context.Comments
            .FirstOrDefault(c => c.Id == commentId)?.Content
            ?? agentEvent.Content.TextPreview
            ?? string.Empty;

        var input = new CommentCreatedPromptInput(
            ActorName: agentEvent.Actor.DisplayName,
            PostContent: context.Post.Content,
            Comments: CommentListFormatter.Format(context.Comments),
            IncomingComment: incomingComment,
            Priority: priority,
            IsMentioned: aiRecipient.Reason == NotificationReason.Mentioned);

        var prompt = _promptComposer.Compose(personaResolution.Persona, input);

        var llmResult = await GetDecisionAsync(agentEvent.ActivityId, parsedAiUserId, prompt, ct);

        if (llmResult.Source == LlmDecisionSource.ProviderFailed)
        {
            _decisionLog.TryLog(new DecisionLogEntry(
                Ts: DateTimeOffset.UtcNow,
                AiUserId: aiRecipient.UserId,
                ActivityId: agentEvent.ActivityId,
                ActivityType: agentEvent.ActivityType.ToString(),
                Outcome: DecisionOutcome.LlmFailed,
                PostId: postId,
                CommentId: commentId,
                Priority: priority.ToString(),
                ErrorDetail: llmResult.ErrorDetail,
                LlmAttempts: llmResult.Attempts,
                PersonaVersion: personaResolution.Persona?.Version,
                PersonaSource: personaResolution.Source,
                SessionLogRef: llmResult.SessionLogRef));
            return;
        }

        if (llmResult.Source == LlmDecisionSource.InvalidJson)
        {
            _decisionLog.TryLog(new DecisionLogEntry(
                Ts: DateTimeOffset.UtcNow,
                AiUserId: aiRecipient.UserId,
                ActivityId: agentEvent.ActivityId,
                ActivityType: agentEvent.ActivityType.ToString(),
                Outcome: DecisionOutcome.LlmFallback,
                PostId: postId,
                CommentId: commentId,
                Priority: priority.ToString(),
                LlmAttempts: llmResult.Attempts,
                PersonaVersion: personaResolution.Persona?.Version,
                PersonaSource: personaResolution.Source,
                SessionLogRef: llmResult.SessionLogRef));
            return;
        }

        var execResult = await _actionExecutor.ExecuteAsync(
            llmResult.Decision, postId, commentId, aiRecipient.UserId, ct);

        var outcome = execResult.Outcome switch
        {
            ActionExecutionOutcome.Executed => DecisionOutcome.Executed,
            ActionExecutionOutcome.Ignored => DecisionOutcome.Ignored,
            ActionExecutionOutcome.Failed => DecisionOutcome.ActionFailed,
            _ => DecisionOutcome.Executed
        };

        if (execResult.Outcome == ActionExecutionOutcome.Failed)
        {
            _opsLog.TryLog(new AgentOpsLogEntry(
                DateTimeOffset.UtcNow, AgentOpsLogLevel.Error, AgentOpsLogSource.Actions,
                $"Action {llmResult.Decision.Action} failed against the API.",
                AiUserId: aiRecipient.UserId, ActivityId: agentEvent.ActivityId,
                Detail: execResult.ErrorDetail));
        }
        else
        {
            _opsLog.TryLog(new AgentOpsLogEntry(
                DateTimeOffset.UtcNow, AgentOpsLogLevel.Info, AgentOpsLogSource.Actions,
                $"Action {llmResult.Decision.Action} {outcome.ToString().ToLowerInvariant()}.",
                AiUserId: aiRecipient.UserId, ActivityId: agentEvent.ActivityId));
        }

        _decisionLog.TryLog(new DecisionLogEntry(
            Ts: DateTimeOffset.UtcNow,
            AiUserId: aiRecipient.UserId,
            ActivityId: agentEvent.ActivityId,
            ActivityType: agentEvent.ActivityType.ToString(),
            Outcome: outcome,
            PostId: postId,
            CommentId: commentId,
            Priority: priority.ToString(),
            Action: JsonNamingPolicy.SnakeCaseLower.ConvertName(llmResult.Decision.Action.ToString()),
            Reasoning: llmResult.Decision.Reasoning,
            Content: llmResult.Decision.Content,
            LlmAttempts: llmResult.Attempts,
            PersonaVersion: personaResolution.Persona?.Version,
            PersonaSource: personaResolution.Source,
            SessionLogRef: llmResult.SessionLogRef,
            ErrorDetail: execResult.Outcome == ActionExecutionOutcome.Failed ? execResult.ErrorDetail : null));
    }

    private async Task ProcessPostMentionedAsync(
        AgentEvent agentEvent,
        AgentEvent.RecipientInfo aiRecipient,
        CancellationToken ct)
    {
        if (aiRecipient.Reason != NotificationReason.Mentioned)
        {
            _logger.LogDebug(
                "PostCreated event {ActivityId}: AI recipient is not mentioned; skipping.",
                agentEvent.ActivityId);
            return;
        }

        if (!Guid.TryParse(agentEvent.Content.Id, out var postId))
        {
            _logger.LogWarning(
                "Agent event {ActivityId} carries an unparseable post id; dropping.",
                agentEvent.ActivityId);
            return;
        }

        var context = await _contextFetcher.FetchAsync(postId, aiRecipient.UserId, ct);
        if (context is null)
        {
            _logger.LogWarning(
                "Context fetch failed for post {PostId}; dropping event {ActivityId}.",
                postId,
                agentEvent.ActivityId);
            _opsLog.TryLog(new AgentOpsLogEntry(
                DateTimeOffset.UtcNow, AgentOpsLogLevel.Warning, AgentOpsLogSource.Pipeline,
                $"Context fetch failed for post {postId}; event dropped.",
                AiUserId: aiRecipient.UserId, ActivityId: agentEvent.ActivityId));
            return;
        }

        var priority = _decayPolicy.Evaluate(agentEvent.Actor.Type, aiRecipient.Priority, context.Comments);
        if (priority == EffectivePriority.Skip)
        {
            _logger.LogInformation(
                "AI↔AI conversation decayed out at event {ActivityId}; not calling the LLM.",
                agentEvent.ActivityId);
            _opsLog.TryLog(new AgentOpsLogEntry(
                DateTimeOffset.UtcNow, AgentOpsLogLevel.Info, AgentOpsLogSource.Pipeline,
                "AI↔AI conversation decayed out; LLM not called.",
                AiUserId: aiRecipient.UserId, ActivityId: agentEvent.ActivityId));
            // skipped_priority is written BEFORE persona resolution; PersonaVersion/PersonaSource are null here by design.
            _decisionLog.TryLog(new DecisionLogEntry(
                Ts: DateTimeOffset.UtcNow,
                AiUserId: aiRecipient.UserId,
                ActivityId: agentEvent.ActivityId,
                ActivityType: agentEvent.ActivityType.ToString(),
                Outcome: DecisionOutcome.SkippedPriority,
                PostId: postId,
                Priority: priority.ToString()));
            return;
        }

        var personaResolution = Guid.TryParse(aiRecipient.UserId, out var parsedAiUserId)
            ? await _personaProvider.GetAsync(parsedAiUserId, ct)
            : new PersonaResolution(null, PersonaSource.Default);

        var input = new PostMentionedPromptInput(
            ActorName: agentEvent.Actor.DisplayName,
            PostContent: context.Post.Content,
            Comments: CommentListFormatter.Format(context.Comments),
            Priority: priority);

        var prompt = _promptComposer.Compose(personaResolution.Persona, input);

        var llmResult = await GetDecisionAsync(agentEvent.ActivityId, parsedAiUserId, prompt, ct);

        if (llmResult.Source == LlmDecisionSource.ProviderFailed)
        {
            _decisionLog.TryLog(new DecisionLogEntry(
                Ts: DateTimeOffset.UtcNow,
                AiUserId: aiRecipient.UserId,
                ActivityId: agentEvent.ActivityId,
                ActivityType: agentEvent.ActivityType.ToString(),
                Outcome: DecisionOutcome.LlmFailed,
                PostId: postId,
                Priority: priority.ToString(),
                ErrorDetail: llmResult.ErrorDetail,
                LlmAttempts: llmResult.Attempts,
                PersonaVersion: personaResolution.Persona?.Version,
                PersonaSource: personaResolution.Source,
                SessionLogRef: llmResult.SessionLogRef));
            return;
        }

        if (llmResult.Source == LlmDecisionSource.InvalidJson)
        {
            _decisionLog.TryLog(new DecisionLogEntry(
                Ts: DateTimeOffset.UtcNow,
                AiUserId: aiRecipient.UserId,
                ActivityId: agentEvent.ActivityId,
                ActivityType: agentEvent.ActivityType.ToString(),
                Outcome: DecisionOutcome.LlmFallback,
                PostId: postId,
                Priority: priority.ToString(),
                LlmAttempts: llmResult.Attempts,
                PersonaVersion: personaResolution.Persona?.Version,
                PersonaSource: personaResolution.Source,
                SessionLogRef: llmResult.SessionLogRef));
            return;
        }

        var decision = llmResult.Decision;
        if (decision.Action == AgentAction.LikeComment)
        {
            decision = new AgentDecision(
                AgentAction.Ignore,
                null,
                "like_comment is not valid for a post mention; treated as ignore");
        }

        var execResult = await _actionExecutor.ExecuteAsync(
            decision, postId, Guid.Empty, aiRecipient.UserId, ct);

        var outcome = execResult.Outcome switch
        {
            ActionExecutionOutcome.Executed => DecisionOutcome.Executed,
            ActionExecutionOutcome.Ignored => DecisionOutcome.Ignored,
            ActionExecutionOutcome.Failed => DecisionOutcome.ActionFailed,
            _ => DecisionOutcome.Executed
        };

        if (execResult.Outcome == ActionExecutionOutcome.Failed)
        {
            _opsLog.TryLog(new AgentOpsLogEntry(
                DateTimeOffset.UtcNow, AgentOpsLogLevel.Error, AgentOpsLogSource.Actions,
                $"Action {decision.Action} failed against the API.",
                AiUserId: aiRecipient.UserId, ActivityId: agentEvent.ActivityId,
                Detail: execResult.ErrorDetail));
        }
        else
        {
            _opsLog.TryLog(new AgentOpsLogEntry(
                DateTimeOffset.UtcNow, AgentOpsLogLevel.Info, AgentOpsLogSource.Actions,
                $"Action {decision.Action} {outcome.ToString().ToLowerInvariant()}.",
                AiUserId: aiRecipient.UserId, ActivityId: agentEvent.ActivityId));
        }

        _decisionLog.TryLog(new DecisionLogEntry(
            Ts: DateTimeOffset.UtcNow,
            AiUserId: aiRecipient.UserId,
            ActivityId: agentEvent.ActivityId,
            ActivityType: agentEvent.ActivityType.ToString(),
            Outcome: outcome,
            PostId: postId,
            Priority: priority.ToString(),
            Action: JsonNamingPolicy.SnakeCaseLower.ConvertName(decision.Action.ToString()),
            Reasoning: decision.Reasoning,
            Content: decision.Content,
            LlmAttempts: llmResult.Attempts,
            PersonaVersion: personaResolution.Persona?.Version,
            PersonaSource: personaResolution.Source,
            SessionLogRef: llmResult.SessionLogRef,
            ErrorDetail: execResult.Outcome == ActionExecutionOutcome.Failed ? execResult.ErrorDetail : null));
    }

    private async Task<LlmDecisionResult> GetDecisionAsync(
        string activityId, Guid aiUserId, ComposedPrompt prompt, CancellationToken ct)
    {
        const int maxAttempts = 2;
        string? lastSessionRef = null;
        string? lastError = null;
        LlmDecisionSource lastSource = LlmDecisionSource.InvalidJson;

        var llm = await _llmSettings.GetAsync(aiUserId, ct);
        var chatProvider = _chatProviderResolver.Resolve(llm.Provider);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var request = new ChatRequest(
                Model: llm.Model,
                Messages: [new ChatMessage(prompt.UserMessage)],
                System: prompt.System,
                MaxTokens: llm.ReplyMaxTokens,
                JsonResponseSchema: AgentDecisionSchema.Json,
                ReasoningEnabled: llm.ThinkingEnabled);

            _opsLog.TryLog(new AgentOpsLogEntry(
                DateTimeOffset.UtcNow, AgentOpsLogLevel.Info, AgentOpsLogSource.LlmProvider,
                $"Requesting {chatProvider.Name} ({llm.Model}), attempt {attempt}/{maxAttempts}.",
                AiUserId: aiUserId.ToString(), ActivityId: activityId));

            var sw = Stopwatch.StartNew();
            var result = await chatProvider.CompleteAsync(request, ct);
            sw.Stop();

            var sessionRef = _sessionLogger.Log(activityId, attempt, request, result);
            if (sessionRef is not null)
            {
                lastSessionRef = sessionRef;
            }

            if (!result.IsSuccess)
            {
                lastError = result.Error?.Message;
                lastSource = LlmDecisionSource.ProviderFailed;
                _logger.LogWarning(
                    "LLM call failed on attempt {Attempt}: {Error}",
                    attempt,
                    result.Error?.Message);
                _opsLog.TryLog(new AgentOpsLogEntry(
                    DateTimeOffset.UtcNow, AgentOpsLogLevel.Error, AgentOpsLogSource.LlmProvider,
                    $"{chatProvider.Name} call failed on attempt {attempt} after {sw.ElapsedMilliseconds} ms.",
                    AiUserId: aiUserId.ToString(), ActivityId: activityId,
                    Detail: result.Error?.Message,
                    StatusCode: result.Error?.StatusCode));
                continue;
            }

            lastSource = LlmDecisionSource.InvalidJson;
            var decision = _decisionParser.Parse(result.Text);
            if (decision is not null)
            {
                _logger.LogInformation(
                    "Agent decision: {Action}. Reasoning: {Reasoning}",
                    decision.Action,
                    decision.Reasoning);
                _opsLog.TryLog(new AgentOpsLogEntry(
                    DateTimeOffset.UtcNow, AgentOpsLogLevel.Info, AgentOpsLogSource.LlmProvider,
                    $"{chatProvider.Name} responded in {sw.ElapsedMilliseconds} ms; decision: {decision.Action}.",
                    AiUserId: aiUserId.ToString(), ActivityId: activityId));
                return new LlmDecisionResult(decision, attempt, LlmDecisionSource.Parsed, null, lastSessionRef);
            }

            _logger.LogWarning(
                "LLM returned an invalid decision on attempt {Attempt}: {RawOutput}",
                attempt,
                result.Text);
            _opsLog.TryLog(new AgentOpsLogEntry(
                DateTimeOffset.UtcNow, AgentOpsLogLevel.Warning, AgentOpsLogSource.LlmProvider,
                $"{chatProvider.Name} returned malformed decision JSON on attempt {attempt}.",
                AiUserId: aiUserId.ToString(), ActivityId: activityId,
                Detail: result.Text is { Length: > 500 } ? result.Text[..500] : result.Text));
        }

        var fallback = new AgentDecision(
            AgentAction.Ignore,
            null,
            "fallback: provider failed or returned invalid JSON twice");

        return new LlmDecisionResult(
            fallback,
            maxAttempts,
            lastSource,
            lastSource == LlmDecisionSource.ProviderFailed ? lastError : null,
            lastSessionRef);
    }
}
