using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.AgentService.Actions;
using AreWeDoomd.AgentService.Ai;
using AreWeDoomd.AgentService.Context;
using AreWeDoomd.AgentService.Decisions;
using AreWeDoomd.AgentService.Prompting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.AgentService.Processing;

public sealed class AgentEventProcessor : BackgroundService
{
    private readonly AgentEventQueue _queue;
    private readonly IContextFetcher _contextFetcher;
    private readonly PriorityDecayPolicy _decayPolicy;
    private readonly IPromptComposer _promptComposer;
    private readonly IChatProvider _chatProvider;
    private readonly DecisionParser _decisionParser;
    private readonly IActionExecutor _actionExecutor;
    private readonly IAiSessionLogger _sessionLogger;
    private readonly AgentServiceOptions _options;
    private readonly ILogger<AgentEventProcessor> _logger;

    public AgentEventProcessor(
        AgentEventQueue queue,
        IContextFetcher contextFetcher,
        PriorityDecayPolicy decayPolicy,
        IPromptComposer promptComposer,
        IChatProvider chatProvider,
        DecisionParser decisionParser,
        IActionExecutor actionExecutor,
        IAiSessionLogger sessionLogger,
        IOptions<AgentServiceOptions> options,
        ILogger<AgentEventProcessor> logger)
    {
        _queue = queue;
        _contextFetcher = contextFetcher;
        _decayPolicy = decayPolicy;
        _promptComposer = promptComposer;
        _chatProvider = chatProvider;
        _decisionParser = decisionParser;
        _actionExecutor = actionExecutor;
        _sessionLogger = sessionLogger;
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

        if (agentEvent.ActivityType != ActivityType.CommentCreated)
        {
            _logger.LogDebug(
                "No agent pipeline for activity type {ActivityType}; skipping {ActivityId}.",
                agentEvent.ActivityType,
                agentEvent.ActivityId);
            return;
        }

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
            return;
        }

        var priority = _decayPolicy.Evaluate(
            agentEvent.Actor.Type,
            aiRecipient.Priority,
            context.Comments);
        if (priority == EffectivePriority.Skip)
        {
            _logger.LogInformation(
                "AI↔AI conversation decayed out at event {ActivityId}; not calling the LLM.",
                agentEvent.ActivityId);
            return;
        }

        string incomingComment = context.Comments
            .FirstOrDefault(c => c.Id == commentId)?.Content
            ?? agentEvent.Content.TextPreview
            ?? string.Empty;

        var input = new CommentCreatedPromptInput(
            ActorName: agentEvent.Actor.DisplayName,
            PostContent: context.Post.Content,
            CommentThread: CommentThreadFormatter.Format(context.Comments),
            IncomingComment: incomingComment,
            Priority: priority);

        var prompt = _promptComposer.Compose(context.Post.AuthorUsername, input);

        var decision = await GetDecisionAsync(agentEvent.ActivityId, prompt, ct);
        await _actionExecutor.ExecuteAsync(decision, postId, commentId, aiRecipient.UserId, ct);
    }

    private async Task<AgentDecision> GetDecisionAsync(string activityId, ComposedPrompt prompt, CancellationToken ct)
    {
        const int maxAttempts = 2;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var request = new ChatRequest(
                Model: _options.Model,
                Messages: [new ChatMessage(prompt.UserMessage)],
                System: prompt.System,
                JsonResponseSchema: AgentDecisionSchema.Json);

            var result = await _chatProvider.CompleteAsync(request, ct);

            _sessionLogger.Log(activityId, attempt, request, result);

            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "LLM call failed on attempt {Attempt}: {Error}",
                    attempt,
                    result.Error?.Message);
                continue;
            }

            var decision = _decisionParser.Parse(result.Text);
            if (decision is not null)
            {
                _logger.LogInformation(
                    "Agent decision: {Action}. Reasoning: {Reasoning}",
                    decision.Action,
                    decision.Reasoning);
                return decision;
            }

            _logger.LogWarning(
                "LLM returned an invalid decision on attempt {Attempt}: {RawOutput}",
                attempt,
                result.Text);
        }

        return new AgentDecision(
            AgentAction.Ignore,
            null,
            "fallback: provider failed or returned invalid JSON twice");
    }
}
