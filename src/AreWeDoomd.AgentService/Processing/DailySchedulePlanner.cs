using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.AgentService.Actions;
using AreWeDoomd.AgentService.Decisions;
using AreWeDoomd.AgentService.Prompting;
using AreWeDoomd.ChatProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.AgentService.Processing;

public sealed class DailySchedulePlanner(
    ScheduleRunQueue queue,
    PromptFileSet prompts,
    DailyPostPlanParser parser,
    IPersonaProvider personaProvider,
    ScheduleDecisionCallbackClient callbackClient,
    IServiceProvider serviceProvider,
    IOptions<AgentServiceOptions> options,
    TimeProvider timeProvider,
    ILogger<DailySchedulePlanner> logger)
    : BackgroundService
{
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
        var chatProvider = serviceProvider.GetRequiredKeyedService<IChatProvider>(options.Value.ChatProvider);

        if (run.Strategy == 1) // SingleCall
        {
            await ProcessSingleCallAsync(run, chatProvider, ct);
            return;
        }

        // ---- Aşama 1: batch puanlama (ucuz model) ----
        var scored = new List<ScoredAccount>();
        var failedItems = new List<ScheduleRunRequestItem>();

        foreach (var chunk in run.Items.Chunk(Math.Max(1, options.Value.ScoringBatchSize)))
        {
            var result = await ScoreBatchAsync(run, chunk, chatProvider, ct);
            if (result is null)
            {
                failedItems.AddRange(chunk);
                continue;
            }

            scored.AddRange(result);
            // Yanıtta eksik kalan item'lar (LLM satır atladı) → tek tek Failed.
            failedItems.AddRange(chunk.Where(i => result.All(s => s.RunItemId != i.RunItemId)));
        }

        foreach (var item in failedItems)
        {
            await callbackClient.SubmitAsync(item.AiUserId, new ScheduleDecisionCallbackClient.CallbackPayload(
                item.RunItemId, 0, null, 0, ScoringModelName(), "LLM scoring failed or omitted this account.", []), ct);
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
                score.RunItemId, score.DesireScore, score.Reasoning, 0, ScoringModelName(), null, []), ct);
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
    }

    private async Task<IReadOnlyList<ScoredAccount>?> ScoreBatchAsync(
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

        for (int attempt = 1; attempt <= 2; attempt++)
        {
            var request = new ChatRequest(
                Model: ScoringModelName(),
                Messages: [new ChatMessage(task)],
                System: null,
                MaxTokens: 150 * chunk.Length, // DefaultMaxTokens=1024 batch çıktısında KESİN yetmez
                Temperature: 0.9,
                JsonResponseSchema: DailyPostScoreSchema.Json);

            var result = await chatProvider.CompleteAsync(request, ct);
            if (result.IsSuccess)
            {
                if (result.Finish == FinishReason.MaxTokens)
                {
                    logger.LogWarning("Scoring batch truncated at MaxTokens (size {Size}); retrying.", chunk.Length);
                    continue; // parse hatası değil — retry sinyali
                }

                var parsed = parser.ParseScores(result.Text, expected);
                if (parsed is not null)
                {
                    return parsed;
                }
            }
        }

        return null;
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
        for (int attempt = 1; attempt <= 2 && plan is null; attempt++)
        {
            var request = new ChatRequest(
                Model: options.Value.Model,
                Messages: [new ChatMessage(task + "\n\n" + prompts.Guardrails)],
                System: system,
                MaxTokens: 600 * postCount,
                Temperature: 0.9,
                JsonResponseSchema: DailyPostComposeSchema.Json);

            var result = await chatProvider.CompleteAsync(request, ct);
            if (result.IsSuccess && result.Finish != FinishReason.MaxTokens)
            {
                plan = parser.ParseCompose(result.Text);
            }
        }

        var payload = plan is null
            ? new ScheduleDecisionCallbackClient.CallbackPayload(
                score.RunItemId, score.DesireScore, score.Reasoning, postCount,
                ModelName(), "LLM composition failed after retries.", [])
            : new ScheduleDecisionCallbackClient.CallbackPayload(
                score.RunItemId, score.DesireScore, score.Reasoning, postCount, ModelName(), null,
                plan.Posts.Select(p => new ScheduleDecisionCallbackClient.CallbackPost(
                    p.Content, p.ScheduledTimeUtc)).ToList());

        await callbackClient.SubmitAsync(item.AiUserId, payload, ct);
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
                var scores = await ScoreBatchAsync(run, [item], chatProvider, ct);
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

    private string ScoringModelName() =>
        string.IsNullOrWhiteSpace(options.Value.ScoringModel) ? options.Value.Model : options.Value.ScoringModel;

    private string ModelName() => options.Value.Model;
}
