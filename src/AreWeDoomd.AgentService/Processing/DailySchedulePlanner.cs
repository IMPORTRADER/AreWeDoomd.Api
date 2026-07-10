using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.AgentService.Actions;
using AreWeDoomd.AgentService.Decisions;
using AreWeDoomd.AgentService.Logging;
using AreWeDoomd.AgentService.Prompting;
using AreWeDoomd.ChatProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.AgentService.Processing;

public sealed class DailySchedulePlanner(
    ScheduleRunQueue queue,
    PromptFileSet prompts,
    DailyPostPlanParser parser,
    IPersonaProvider personaProvider,
    IScheduleDecisionCallbackClient callbackClient,
    IChatProviderResolver chatProviderResolver,
    ILlmSettingsProvider llmSettingsProvider,
    IOptions<AgentServiceOptions> options,
    TimeProvider timeProvider,
    IAgentOpsLogWriter opsLog,
    ILogger<DailySchedulePlanner> logger)
    : BackgroundService
{
    // ScheduleRunItem.ErrorDetail column limit (ScheduleRunItemConfiguration.cs: HasMaxLength(1000)).
    private const int ErrorDetailMaxLength = 1000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            ScheduleRunRequest run;
            try
            {
                run = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await ProcessRunAsync(run, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled failure processing schedule run {RunId}", run.RunId);
            }
        }
    }

    public async Task ProcessRunAsync(ScheduleRunRequest run, CancellationToken ct)
    {
        var actingId = run.Items.Count > 0 ? run.Items[0].AiUserId : Guid.Empty;
        var llm = await llmSettingsProvider.GetAsync(actingId, ct);
        var chatProvider = chatProviderResolver.Resolve(llm.Provider);
        opsLog.TryLog(new AgentOpsLogEntry(
            timeProvider.GetUtcNow(), AgentOpsLogLevel.Info, AgentOpsLogSource.Scheduling,
            $"Schedule run {run.RunId}: planning posts for {run.Items.Count} account(s) with {chatProvider.Name}."));

        if (run.Strategy == 1) // SingleCall
        {
            await ProcessSingleCallAsync(run, chatProvider, ct);
            opsLog.TryLog(new AgentOpsLogEntry(
                timeProvider.GetUtcNow(), AgentOpsLogLevel.Info, AgentOpsLogSource.Scheduling,
                $"Schedule run {run.RunId} completed (single-call strategy)."));
            return;
        }

        // ---- Aşama 1: batch puanlama (ucuz model) ----
        var scored = new List<ScoredAccount>();
        var failedItems = new List<(ScheduleRunRequestItem Item, string Error)>();

        foreach (var chunk in run.Items.Chunk(Math.Max(1, options.Value.ScoringBatchSize)))
        {
            var (result, error) = await ScoreBatchAsync(run, chunk, chatProvider, ct);
            if (result is null)
            {
                failedItems.AddRange(chunk.Select(i => (i, error ?? "LLM scoring failed.")));
                continue;
            }

            scored.AddRange(result);
            // Yanıtta eksik kalan item'lar (LLM satır atladı) → tek tek Failed.
            failedItems.AddRange(chunk
                .Where(i => result.All(s => s.RunItemId != i.RunItemId))
                .Select(i => (i, "LLM scoring omitted this account from its response.")));
        }

        foreach (var (item, error) in failedItems)
        {
            await callbackClient.SubmitAsync(item.AiUserId, new ScheduleDecisionCallbackClient.CallbackPayload(
                item.RunItemId, 0, null, 0, ScoringModelName(run),
                Truncate($"Scoring failed: {error}"), []), ct);
            opsLog.TryLog(new AgentOpsLogEntry(
                timeProvider.GetUtcNow(), AgentOpsLogLevel.Warning, AgentOpsLogSource.Scheduling,
                $"Scoring failed for @{item.Username}.",
                AiUserId: item.AiUserId.ToString(), AiUsername: item.Username,
                Detail: error));
        }

        // ---- Aşama 2: yalnız eşiği geçenler, hesap başına kompozisyon ----
        // Threshold payload'da YALNIZ kapılama için; otoriter kontrol API'de snapshot ile.
        var itemById = run.Items.ToDictionary(i => i.RunItemId);
        var passed = scored.Where(s => s.DesireScore >= run.Threshold).ToList();
        var below = scored.Where(s => s.DesireScore < run.Threshold).ToList();

        foreach (var score in below)
        {
            var item = itemById[score.RunItemId];
            await callbackClient.SubmitAsync(item.AiUserId, new ScheduleDecisionCallbackClient.CallbackPayload(
                score.RunItemId, score.DesireScore, score.Reasoning, 0, ScoringModelName(run), null, []), ct);
            opsLog.TryLog(new AgentOpsLogEntry(
                timeProvider.GetUtcNow(), AgentOpsLogLevel.Info, AgentOpsLogSource.Scheduling,
                $"@{item.Username} below desire threshold (score {score.DesireScore}); no post planned.",
                AiUserId: item.AiUserId.ToString(), AiUsername: item.Username));
        }

        using var semaphore = new SemaphoreSlim(Math.Max(1, options.Value.MaxParallelCompositions));
        var tasks = passed.Select(async score =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                await ComposeAndSubmitAsync(run, itemById[score.RunItemId], score, chatProvider, ct);
            }
            finally
            {
                semaphore.Release();
            }
        });
        await Task.WhenAll(tasks);
        opsLog.TryLog(new AgentOpsLogEntry(
            timeProvider.GetUtcNow(), AgentOpsLogLevel.Info, AgentOpsLogSource.Scheduling,
            $"Schedule run {run.RunId} completed: {passed.Count} account(s) passed threshold, {below.Count} below, {failedItems.Count} failed."));
    }

    private async Task<(IReadOnlyList<ScoredAccount>? Scores, string? Error)> ScoreBatchAsync(
        ScheduleRunRequest run, ScheduleRunRequestItem[] chunk, IChatProvider chatProvider, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        string rows = string.Join("\n", chunk.Select(i =>
        {
            string persona = string.IsNullOrWhiteSpace(i.PersonaSummary)
                ? "no persona: curious, friendly tech enthusiast"
                : i.PersonaSummary;
            string activity = i.LastPostAtUtc is null
                ? $"{i.PostsLast3Days} posts in last 3 days, never posted"
                : $"{i.PostsLast3Days} posts in last 3 days, last post {(int)(now - i.LastPostAtUtc.Value).TotalHours} hours ago";
            return $"- runItemId: {i.RunItemId} | name: {i.Username} | persona: {persona} | activity: {activity}";
        }));

        string task = prompts.DailyPostScoreTask
            .Replace("{{today}}", now.ToString("yyyy-MM-dd"))
            .Replace("{{weekday}}", now.DayOfWeek.ToString())
            .Replace("{{account_count}}", chunk.Length.ToString())
            .Replace("{{max_posts}}", run.MaxPostsPerAccount.ToString())
            .Replace("{{account_rows}}", rows);

        var expected = chunk.Select(i => i.RunItemId).ToHashSet();

        string? lastError = null;
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            var request = new ChatRequest(
                Model: string.IsNullOrWhiteSpace(run.ScoringModel) ? run.Model : run.ScoringModel,
                Messages: [new ChatMessage(task)],
                System: null,
                MaxTokens: run.ScoringTokensPerAccount * chunk.Length,
                Temperature: 0.9,
                JsonResponseSchema: DailyPostScoreSchema.Json,
                ReasoningEnabled: run.ThinkingEnabled);

            var result = await chatProvider.CompleteAsync(request, ct);
            if (result.IsSuccess)
            {
                if (result.Finish == FinishReason.MaxTokens)
                {
                    lastError = "Token budget exhausted: scoring output was truncated at max tokens.";
                    logger.LogWarning("Scoring batch truncated at MaxTokens (size {Size}); retrying.", chunk.Length);
                    continue;
                }

                var parsed = parser.ParseScores(result.Text, expected);
                if (parsed is not null)
                {
                    return (parsed, null);
                }

                lastError = "Scoring response could not be parsed as valid JSON scores.";
                logger.LogWarning("Scoring batch parse failed (size {Size}, attempt {Attempt}).", chunk.Length, attempt);
            }
            else
            {
                lastError = result.Error!.Message;
                logger.LogWarning(
                    "Scoring LLM call failed (attempt {Attempt}/2, provider {Provider}): {Message}",
                    attempt, result.Error.Provider, result.Error.Message);
            }
        }

        return (null, lastError ?? "LLM scoring failed.");
    }

    private async Task ComposeAndSubmitAsync(
        ScheduleRunRequest run, ScheduleRunRequestItem item, ScoredAccount score,
        IChatProvider chatProvider, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        int postCount = Math.Clamp(score.HypotheticalPostCount, 1, run.MaxPostsPerAccount);

        // Kalan pencere daralmışsa postCount'u sığacak kadar indir (min 30 dk/gönderi).
        var remaining = run.WindowEndUtc - now;
        int fitCount = Math.Max(1, (int)(remaining.TotalMinutes / 30));
        postCount = Math.Min(postCount, fitCount);

        // IPersonaProvider.GetAsync takes Guid and returns PersonaResolution
        var resolution = await personaProvider.GetAsync(item.AiUserId, ct);
        string system = resolution.Persona is null
            ? PersonaPromptRenderer.DefaultPersonality
            : PersonaPromptRenderer.Render(resolution.Persona);

        string task = prompts.DailyPostComposeTask
            .Replace("{{today}}", now.ToString("yyyy-MM-dd"))
            .Replace("{{weekday}}", now.DayOfWeek.ToString())
            .Replace("{{post_count}}", postCount.ToString())
            .Replace("{{length_guide}}", run.PostLengthGuide.ToString())
            .Replace("{{window_start_utc}}", run.WindowStartUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"))
            .Replace("{{window_end_utc}}", run.WindowEndUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"));

        ComposedPlan? plan = null;
        string? lastError = null;
        for (int attempt = 1; attempt <= 2 && plan is null; attempt++)
        {
            var request = new ChatRequest(
                Model: run.Model,
                Messages: [new ChatMessage(task + "\n\n" + prompts.Guardrails)],
                System: system,
                MaxTokens: run.CompositionTokensPerPost * postCount,
                Temperature: 0.9,
                JsonResponseSchema: DailyPostComposeSchema.Json,
                ReasoningEnabled: run.ThinkingEnabled);

            var result = await chatProvider.CompleteAsync(request, ct);
            if (result.IsSuccess && result.Finish != FinishReason.MaxTokens)
            {
                plan = parser.ParseCompose(result.Text);
                if (plan is null)
                {
                    lastError = "Compose response could not be parsed as a valid plan.";
                }
            }
            else
            {
                lastError = result.IsSuccess
                    ? "Token budget exhausted: compose output was truncated at max tokens."
                    : result.Error!.Message;
                logger.LogWarning(
                    "Compose LLM call failed for {Username} (attempt {Attempt}/2): {Message}",
                    item.Username, attempt, lastError);
            }
        }

        var payload = plan is null
            ? new ScheduleDecisionCallbackClient.CallbackPayload(
                score.RunItemId, score.DesireScore, score.Reasoning, postCount,
                ModelName(run), Truncate($"Composition failed: {lastError ?? "unknown error"}"), [])
            : new ScheduleDecisionCallbackClient.CallbackPayload(
                score.RunItemId, score.DesireScore, score.Reasoning, postCount, ModelName(run), null,
                plan.Posts.Select(p => new ScheduleDecisionCallbackClient.CallbackPost(
                    p.Content, p.ScheduledTimeUtc)).ToList());

        await callbackClient.SubmitAsync(item.AiUserId, payload, ct);

        if (plan is not null)
        {
            opsLog.TryLog(new AgentOpsLogEntry(
                timeProvider.GetUtcNow(), AgentOpsLogLevel.Info, AgentOpsLogSource.Scheduling,
                $"Post composed and scheduled for @{item.Username} (score {score.DesireScore}).",
                AiUserId: item.AiUserId.ToString(), AiUsername: item.Username));
        }
        else
        {
            opsLog.TryLog(new AgentOpsLogEntry(
                timeProvider.GetUtcNow(), AgentOpsLogLevel.Warning, AgentOpsLogSource.Scheduling,
                $"Composition failed for @{item.Username} (score {score.DesireScore}).",
                AiUserId: item.AiUserId.ToString(), AiUsername: item.Username,
                Detail: lastError));
        }
    }

    private async Task ProcessSingleCallAsync(
        ScheduleRunRequest run, IChatProvider chatProvider, CancellationToken ct)
    {
        // SingleCall (admin ayarı): eşik altı kalsa bile içerik üretilir ki admin
        // "yazacak olduklarını" görebilsin — token maliyeti pahasına görünürlük.
        using var semaphore = new SemaphoreSlim(Math.Max(1, options.Value.MaxParallelCompositions));
        var tasks = run.Items.Select(async item =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var (scores, _) = await ScoreBatchAsync(run, [item], chatProvider, ct);
                var score = scores?.FirstOrDefault()
                    ?? new ScoredAccount(item.RunItemId, "scoring failed; defaulting", 0, 1);
                await ComposeAndSubmitAsync(run, item, score, chatProvider, ct);
            }
            finally
            {
                semaphore.Release();
            }
        });
        await Task.WhenAll(tasks);
    }

    private static string ScoringModelName(ScheduleRunRequest run) =>
        string.IsNullOrWhiteSpace(run.ScoringModel) ? run.Model : run.ScoringModel;

    private static string ModelName(ScheduleRunRequest run) => run.Model;

    // ScheduleRunItem.ErrorDetail kolon sınırına sığması için.
    private static string Truncate(string value) =>
        value.Length <= ErrorDetailMaxLength ? value : value[..ErrorDetailMaxLength];
}
