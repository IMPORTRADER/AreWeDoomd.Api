# M1: Decision Log Write Path (JSONL on Disk) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist every agent decision (action, reasoning, outcome) as one JSONL line on disk, including queue-dropped events, so the M3/M4 dashboard can answer "what did this AI do and why" (spec: `../../../docs/specs/ai-fleet-management.md` §3.1 — the spec lives in the repo root above this git repo).

**Architecture:** A new `Logging/` folder in AgentService. `DecisionLogWriter` is a singleton `BackgroundService` exposing non-blocking `IDecisionLogWriter.TryLog`; entries flow through a bounded channel to a background pump that appends JSON lines to daily UTC files via `DecisionLogFileAppender` (which also owns 30-day retention). `AgentEventProcessor` writes ONE consolidated entry per processed event (`ActionExecutor` now returns an outcome instead of `Task`), and `AgentNotificationListener` writes a `dropped` entry when the event queue rejects. `AiSessionLogger.Log` now returns the relative session-file ref so entries can link decision → full prompt/response.

**Tech Stack:** .NET 10, System.Threading.Channels, System.Text.Json (no new packages). Tests: xunit + Moq + Shouldly (existing stack).

## Global Constraints

- Git repo root is `AreWeDoomd.Api/` (outer folder is not a git repo). All commands run from there. Branch: continue on `feature/m0-extract-chatproviders`'s successor or a new `feature/m1-decision-log` branched from it — the controller decides at execution time; M1 code does not depend on M0's moves beyond `using` lines that already exist.
- Backend rules (AGENTS.md): one class per file; braces always required; namespaces mirror folders.
- Commit messages: plain, NO `Co-Authored-By` trailer.
- **Invariant (spec §3.1): a log-write failure must never stall or fail decision processing.** `TryLog` never blocks, never throws.
- File format: `{RootPath}/decisions-{yyyy-MM-dd}.jsonl`, date = **UTC**. JSON: camelCase properties, nulls omitted, `outcome` values snake_case (`executed`, `skipped_priority`, `ignored`, `llm_failed`, `llm_fallback`, `action_failed`, `dropped`).
- Writer opens files `FileMode.Append, FileAccess.Write, FileShare.Read`.
- Retention: 30 days, deleted ONLY by this writer (Api mount is read-only).
- Config: section `DecisionLog`, `RootPath` default `logs/agent-decisions`; env var `DECISION_LOG_ROOT` maps onto `DecisionLog:RootPath` in `Program.cs` (same pattern as `GEMINI_API_KEY`). Never a committed absolute path.
- Known deviations from the spec's example JSON, decided here: (a) `aiUsername` is omitted — `AgentEvent.RecipientInfo` carries only `UserId`; the dashboard joins username by id in M3. (b) The channel uses `DropWrite` (not DropOldest) so rejected writes are countable and logged — loss is visible, per the spec's "log-loss is visible, not silent" requirement. (c) Events that exit before a decision (unsupported ActivityType, unparseable ids, context-fetch failure) are NOT logged in M1 — the spec's outcome list doesn't cover them; extending observability there is a later milestone.
- After every task: `dotnet build` and `dotnet test tests/AreWeDoomd.UnitTests` green before committing.

---

### Task 1: DecisionLogEntry, DecisionOutcome, serializer, options

**Files:**
- Create: `src/AreWeDoomd.AgentService/Logging/DecisionOutcome.cs`
- Create: `src/AreWeDoomd.AgentService/Logging/DecisionLogEntry.cs`
- Create: `src/AreWeDoomd.AgentService/Logging/DecisionLogSerializer.cs`
- Create: `src/AreWeDoomd.AgentService/Logging/DecisionLogOptions.cs`
- Test: `tests/AreWeDoomd.UnitTests/AgentService/Logging/DecisionLogSerializerTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces (used by every later task):

```csharp
public enum DecisionOutcome { Executed, SkippedPriority, Ignored, LlmFailed, LlmFallback, ActionFailed, Dropped }

public sealed record DecisionLogEntry(
    DateTimeOffset Ts,
    string AiUserId,
    string ActivityId,
    string ActivityType,
    DecisionOutcome Outcome,
    string? Action = null,
    string? Reasoning = null,
    string? Content = null,
    Guid? PostId = null,
    Guid? CommentId = null,
    string? Priority = null,
    string? ErrorDetail = null,
    int? LlmAttempts = null,
    int? PersonaVersion = null,
    string? PersonaSource = null,
    string? SessionLogRef = null);

public static class DecisionLogSerializer
{
    public static string Serialize(DecisionLogEntry entry); // single line, no trailing newline
}
```

`PersonaVersion`/`PersonaSource` stay null until M2 — declared now so the JSONL schema never changes shape.

- [ ] **Step 1: Write the failing tests**

`tests/AreWeDoomd.UnitTests/AgentService/Logging/DecisionLogSerializerTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/AreWeDoomd.UnitTests --filter "FullyQualifiedName~DecisionLogSerializer"`
Expected: FAIL — compile error, types don't exist.

- [ ] **Step 3: Implement**

`DecisionOutcome.cs`:

```csharp
namespace AreWeDoomd.AgentService.Logging;

public enum DecisionOutcome
{
    Executed = 0,
    SkippedPriority = 1,
    Ignored = 2,
    LlmFailed = 3,
    LlmFallback = 4,
    ActionFailed = 5,
    Dropped = 6
}
```

`DecisionLogEntry.cs`: the record exactly as in the Interfaces block, with `namespace AreWeDoomd.AgentService.Logging;`.

`DecisionLogSerializer.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AreWeDoomd.AgentService.Logging;

public static class DecisionLogSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    public static string Serialize(DecisionLogEntry entry)
    {
        return JsonSerializer.Serialize(entry, Options);
    }
}
```

`DecisionLogOptions.cs`:

```csharp
namespace AreWeDoomd.AgentService.Logging;

public sealed class DecisionLogOptions
{
    public const string SectionName = "DecisionLog";

    /// <summary>Directory for decisions-{yyyy-MM-dd}.jsonl files. Relative paths resolve against the process working directory (same convention as the ai-sessions logs).</summary>
    public string RootPath { get; set; } = Path.Combine("logs", "agent-decisions");

    /// <summary>Daily files older than this are deleted by the writer.</summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>Bounded capacity of the in-memory entry channel.</summary>
    public int QueueCapacity { get; set; } = 1000;
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/AreWeDoomd.UnitTests --filter "FullyQualifiedName~DecisionLogSerializer"`
Expected: 2/2 PASS.

- [ ] **Step 5: Commit**

```powershell
git add -A; git commit -m "feat: add decision log entry model and JSONL serializer"
```

---

### Task 2: DecisionLogFileAppender (daily files + retention)

**Files:**
- Create: `src/AreWeDoomd.AgentService/Logging/DecisionLogFileAppender.cs`
- Test: `tests/AreWeDoomd.UnitTests/AgentService/Logging/DecisionLogFileAppenderTests.cs`

**Interfaces:**
- Consumes: `DecisionLogEntry`, `DecisionLogSerializer` (Task 1).
- Produces:

```csharp
public sealed class DecisionLogFileAppender
{
    public DecisionLogFileAppender(string rootPath);
    public void Append(DecisionLogEntry entry);        // writes Serialize(entry) + '\n' to decisions-{entry.Ts UTC date}.jsonl
    public int CleanupOldFiles(int retentionDays);     // deletes decisions-*.jsonl older than retentionDays (by filename date, UTC); returns count deleted
}
```

- [ ] **Step 1: Write the failing tests**

```csharp
using AreWeDoomd.AgentService.Logging;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.AgentService.Logging;

public sealed class DecisionLogFileAppenderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "awd-declog-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) { Directory.Delete(_dir, recursive: true); }
    }

    private static DecisionLogEntry Entry(DateTimeOffset ts) =>
        new(ts, "ai-1", "act-1", "CommentCreated", DecisionOutcome.Executed);

    [Fact]
    public void Append_ShouldCreateDailyFileAndWriteOneLine()
    {
        var appender = new DecisionLogFileAppender(_dir);
        var ts = DateTimeOffset.Parse("2026-07-04T23:59:00Z");

        appender.Append(Entry(ts));
        appender.Append(Entry(ts));

        string file = Path.Combine(_dir, "decisions-2026-07-04.jsonl");
        File.Exists(file).ShouldBeTrue();
        File.ReadAllLines(file).Length.ShouldBe(2);
    }

    [Fact]
    public void Append_ShouldRotateByUtcDate()
    {
        var appender = new DecisionLogFileAppender(_dir);

        appender.Append(Entry(DateTimeOffset.Parse("2026-07-04T23:59:00Z")));
        appender.Append(Entry(DateTimeOffset.Parse("2026-07-05T00:01:00Z")));

        File.Exists(Path.Combine(_dir, "decisions-2026-07-04.jsonl")).ShouldBeTrue();
        File.Exists(Path.Combine(_dir, "decisions-2026-07-05.jsonl")).ShouldBeTrue();
    }

    [Fact]
    public void CleanupOldFiles_ShouldDeleteOnlyExpiredFiles()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "decisions-2020-01-01.jsonl"), "old\n");
        string todayFile = $"decisions-{DateTime.UtcNow:yyyy-MM-dd}.jsonl";
        File.WriteAllText(Path.Combine(_dir, todayFile), "new\n");
        File.WriteAllText(Path.Combine(_dir, "not-a-decision.txt"), "keep\n");
        var appender = new DecisionLogFileAppender(_dir);

        int deleted = appender.CleanupOldFiles(retentionDays: 30);

        deleted.ShouldBe(1);
        File.Exists(Path.Combine(_dir, "decisions-2020-01-01.jsonl")).ShouldBeFalse();
        File.Exists(Path.Combine(_dir, todayFile)).ShouldBeTrue();
        File.Exists(Path.Combine(_dir, "not-a-decision.txt")).ShouldBeTrue();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/AreWeDoomd.UnitTests --filter "FullyQualifiedName~DecisionLogFileAppender"`
Expected: FAIL — type doesn't exist.

- [ ] **Step 3: Implement**

```csharp
using System.Globalization;
using System.Text;

namespace AreWeDoomd.AgentService.Logging;

/// <summary>
/// Synchronous file operations for the decision log: daily-file append and
/// retention cleanup. Not thread-safe by itself — DecisionLogWriter's single
/// pump is the only caller at runtime.
/// </summary>
public sealed class DecisionLogFileAppender
{
    private const string FilePrefix = "decisions-";
    private const string FileSuffix = ".jsonl";

    private readonly string _rootPath;

    public DecisionLogFileAppender(string rootPath)
    {
        _rootPath = rootPath;
    }

    public void Append(DecisionLogEntry entry)
    {
        Directory.CreateDirectory(_rootPath);
        string path = Path.Combine(
            _rootPath,
            $"{FilePrefix}{entry.Ts.UtcDateTime:yyyy-MM-dd}{FileSuffix}");

        using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.WriteLine(DecisionLogSerializer.Serialize(entry));
    }

    public int CleanupOldFiles(int retentionDays)
    {
        if (!Directory.Exists(_rootPath))
        {
            return 0;
        }

        DateTime cutoff = DateTime.UtcNow.Date.AddDays(-retentionDays);
        int deleted = 0;

        foreach (string file in Directory.EnumerateFiles(_rootPath, $"{FilePrefix}*{FileSuffix}"))
        {
            string datePart = Path.GetFileName(file)[FilePrefix.Length..^FileSuffix.Length];
            if (DateTime.TryParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime fileDate)
                && fileDate.Date < cutoff)
            {
                File.Delete(file);
                deleted++;
            }
        }

        return deleted;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/AreWeDoomd.UnitTests --filter "FullyQualifiedName~DecisionLogFileAppender"`
Expected: 3/3 PASS.

- [ ] **Step 5: Commit**

```powershell
git add -A; git commit -m "feat: add decision log daily-file appender with retention"
```

---

### Task 3: DecisionLogWriter (non-blocking channel pump, hosted) + DI + env mapping

**Files:**
- Create: `src/AreWeDoomd.AgentService/Logging/IDecisionLogWriter.cs`
- Create: `src/AreWeDoomd.AgentService/Logging/DecisionLogWriter.cs`
- Modify: `src/AreWeDoomd.AgentService/Program.cs`
- Test: `tests/AreWeDoomd.UnitTests/AgentService/Logging/DecisionLogWriterTests.cs`

**Interfaces:**
- Consumes: Tasks 1–2.
- Produces (all later tasks depend on this exact contract):

```csharp
public interface IDecisionLogWriter
{
    /// <summary>Never blocks, never throws. False = entry was rejected (channel full/closed) — already counted and logged.</summary>
    bool TryLog(DecisionLogEntry entry);
}
```

`DecisionLogWriter : BackgroundService, IDecisionLogWriter` — bounded channel (`DropWrite`, capacity from options), single pump task appends via `DecisionLogFileAppender` (each append in try/catch → `LogWarning`, never rethrow), rejected `TryWrite`s increment a counter and `LogWarning` (rate: log every rejection; volume is inherently bounded by the queue being full). Retention: `CleanupOldFiles` on startup and then every 24h via `PeriodicTimer`, also try/catch-protected.

- [ ] **Step 1: Write the failing test**

```csharp
using AreWeDoomd.AgentService.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.AgentService.Logging;

public sealed class DecisionLogWriterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "awd-declogw-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) { Directory.Delete(_dir, recursive: true); }
    }

    [Fact]
    public async Task TryLog_ShouldEventuallyAppendLineToDailyFile()
    {
        var options = Options.Create(new DecisionLogOptions { RootPath = _dir });
        using var writer = new DecisionLogWriter(options, NullLogger<DecisionLogWriter>.Instance);
        await writer.StartAsync(CancellationToken.None);

        bool accepted = writer.TryLog(new DecisionLogEntry(
            DateTimeOffset.UtcNow, "ai-1", "act-1", "CommentCreated", DecisionOutcome.Executed));

        accepted.ShouldBeTrue();
        string file = Path.Combine(_dir, $"decisions-{DateTime.UtcNow:yyyy-MM-dd}.jsonl");
        await WaitForFileLineAsync(file);
        await writer.StopAsync(CancellationToken.None);
        File.ReadAllLines(file).Length.ShouldBe(1);
    }

    private static async Task WaitForFileLineAsync(string file)
    {
        for (int i = 0; i < 100; i++)
        {
            if (File.Exists(file) && new FileInfo(file).Length > 0) { return; }
            await Task.Delay(20);
        }
        throw new TimeoutException($"No line appeared in {file}.");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AreWeDoomd.UnitTests --filter "FullyQualifiedName~DecisionLogWriterTests"`
Expected: FAIL — types don't exist.

- [ ] **Step 3: Implement**

`IDecisionLogWriter.cs`: interface exactly as in the Interfaces block, `namespace AreWeDoomd.AgentService.Logging;`.

`DecisionLogWriter.cs`:

```csharp
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AreWeDoomd.AgentService.Logging;

/// <summary>
/// Non-blocking JSONL decision log. TryLog enqueues; a single background pump
/// appends to daily files. Invariant: a log failure never stalls or fails
/// decision processing — TryLog never blocks and never throws.
/// </summary>
public sealed class DecisionLogWriter : BackgroundService, IDecisionLogWriter
{
    private readonly Channel<DecisionLogEntry> _channel;
    private readonly DecisionLogFileAppender _appender;
    private readonly DecisionLogOptions _options;
    private readonly ILogger<DecisionLogWriter> _logger;
    private long _rejectedCount;

    public DecisionLogWriter(IOptions<DecisionLogOptions> options, ILogger<DecisionLogWriter> logger)
    {
        _options = options.Value;
        _logger = logger;
        _appender = new DecisionLogFileAppender(_options.RootPath);
        _channel = Channel.CreateBounded<DecisionLogEntry>(new BoundedChannelOptions(_options.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true
        });
    }

    public bool TryLog(DecisionLogEntry entry)
    {
        if (_channel.Writer.TryWrite(entry))
        {
            return true;
        }

        long total = Interlocked.Increment(ref _rejectedCount);
        _logger.LogWarning(
            "Decision log channel full; entry for activity {ActivityId} dropped ({Total} rejected so far).",
            entry.ActivityId, total);
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.WhenAll(PumpAsync(stoppingToken), RetentionLoopAsync(stoppingToken));
    }

    private async Task PumpAsync(CancellationToken ct)
    {
        try
        {
            await foreach (DecisionLogEntry entry in _channel.Reader.ReadAllAsync(ct))
            {
                try
                {
                    _appender.Append(entry);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to append decision log entry for activity {ActivityId}.", entry.ActivityId);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private async Task RetentionLoopAsync(CancellationToken ct)
    {
        RunCleanup();
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                RunCleanup();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private void RunCleanup()
    {
        try
        {
            int deleted = _appender.CleanupOldFiles(_options.RetentionDays);
            if (deleted > 0)
            {
                _logger.LogInformation("Decision log retention deleted {Count} expired file(s).", deleted);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Decision log retention cleanup failed.");
        }
    }
}
```

`Program.cs` — three additions:

1. After the `OPENROUTER_API_KEY` mapping block, same pattern:

```csharp
// Map the conventional DECISION_LOG_ROOT environment variable onto the
// decision log's config key, same pattern as the API-key mappings above.
string? decisionLogRoot = Environment.GetEnvironmentVariable("DECISION_LOG_ROOT");
if (!string.IsNullOrWhiteSpace(decisionLogRoot))
{
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["DecisionLog:RootPath"] = decisionLogRoot
    });
}
```

2. With the other `Configure` calls: `builder.Services.Configure<DecisionLogOptions>(builder.Configuration.GetSection(DecisionLogOptions.SectionName));`
3. Registrations (single instance serving both roles):

```csharp
builder.Services.AddSingleton<DecisionLogWriter>();
builder.Services.AddSingleton<IDecisionLogWriter>(sp => sp.GetRequiredService<DecisionLogWriter>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<DecisionLogWriter>());
```

Plus `using AreWeDoomd.AgentService.Logging;` in the using block.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/AreWeDoomd.UnitTests --filter "FullyQualifiedName~DecisionLog"`
Expected: 6/6 PASS (Tasks 1–3 tests). Then `dotnet build` — whole solution green.

- [ ] **Step 5: Commit**

```powershell
git add -A; git commit -m "feat: add non-blocking decision log writer hosted service"
```

---

### Task 4: AiSessionLogger returns the session-file ref

**Files:**
- Modify: `src/AreWeDoomd.AgentService/Ai/IAiSessionLogger.cs`
- Modify: `src/AreWeDoomd.AgentService/Ai/AiSessionLogger.cs`
- Modify: `src/AreWeDoomd.AgentService/Processing/AgentEventProcessor.cs` (call site compiles; ref is captured in Task 5 — here just discard with `_ =` if needed; simplest: change nothing at the call site because a `void`→`string` return change compiles at existing call sites unchanged)
- Test: `tests/AreWeDoomd.UnitTests/AgentService/Ai/AiSessionLoggerTests.cs` (create)

**Interfaces:**
- Consumes: `ChatRequest`, `ChatResult` (from `AreWeDoomd.ChatProviders` after M0; from `AreWeDoomd.AgentService.Ai` if M0 hasn't merged — use whichever namespace the current branch compiles with).
- Produces:

```csharp
public interface IAiSessionLogger
{
    /// <summary>Writes the session file and returns its ref relative to logs/ai-sessions, e.g. "2026-07-04/act-1_attempt1_123105.txt". Null if the file write failed (console log still emitted).</summary>
    string? Log(string activityId, int attempt, ChatRequest request, ChatResult result);
}
```

- [ ] **Step 1: Write the failing test**

`AiSessionLoggerTests.cs` (note: `AiSessionLogger` writes under the relative path `logs/ai-sessions` from the current working directory — the test asserts on the returned ref and the file's existence under that path, then cleans up):

```csharp
using AreWeDoomd.AgentService.Ai;
using AreWeDoomd.ChatProviders; // or AreWeDoomd.AgentService.Ai pre-M0 — match the branch
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace AreWeDoomd.UnitTests.AgentService.Ai;

public sealed class AiSessionLoggerTests
{
    [Fact]
    public void Log_ShouldReturnRelativeSessionRefPointingAtWrittenFile()
    {
        var logger = new AiSessionLogger(NullLogger<AiSessionLogger>.Instance);
        var request = new ChatRequest("model-x", [new ChatMessage("hello")]);
        var result = ChatResult.Ok("hi", new TokenUsage(1, 2), FinishReason.Stop);

        string? sessionRef = logger.Log("act-ref-test", 1, request, result);

        sessionRef.ShouldNotBeNull();
        sessionRef.ShouldStartWith($"{DateTime.UtcNow:yyyy-MM-dd}/");
        string fullPath = Path.Combine("logs", "ai-sessions", sessionRef.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(fullPath).ShouldBeTrue();
        File.Delete(fullPath);
    }
}
```

(If `ChatResult.Ok`'s exact factory signature differs, read `ChatResult.cs` and adjust the test construction — the assertion targets are the ref format and file existence, not the result shape.)

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AreWeDoomd.UnitTests --filter "FullyQualifiedName~AiSessionLoggerTests"`
Expected: FAIL — `Log` returns void, assignment doesn't compile.

- [ ] **Step 3: Implement**

- `IAiSessionLogger.Log` return type: `void` → `string?` (doc comment as in Interfaces block).
- `AiSessionLogger.Log`: `WriteSessionFile` currently computes `dir` and `fileName` internally and returns void. Change `WriteSessionFile` to return `string?`: on success return `$"{DateTime.UtcNow:yyyy-MM-dd}/{fileName}"`; wrap the `Directory.CreateDirectory` + `File.WriteAllText` in try/catch returning `null` on failure (log write must not throw into the decision path — this hardening is in-scope for the signature change). `Log` returns that value. Keep the console `LogInformation` unchanged.
- Note: the date string must be captured ONCE and used for both the directory and the returned ref (a midnight race between two `DateTime.UtcNow` reads would produce a ref pointing at the wrong folder).

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/AreWeDoomd.UnitTests`
Expected: all pass — existing `Mock<IAiSessionLogger>` setups keep working (Moq returns `null` for unstubbed `string?` methods).

- [ ] **Step 5: Commit**

```powershell
git add -A; git commit -m "feat: AiSessionLogger returns the relative session-file ref"
```

---

### Task 5: ActionExecutor returns an outcome

**Files:**
- Create: `src/AreWeDoomd.AgentService/Actions/ActionExecutionOutcome.cs`
- Create: `src/AreWeDoomd.AgentService/Actions/ActionExecutionResult.cs`
- Modify: `src/AreWeDoomd.AgentService/Actions/IActionExecutor.cs`
- Modify: `src/AreWeDoomd.AgentService/Actions/ActionExecutor.cs`
- Modify: `tests/AreWeDoomd.UnitTests/AgentService/Actions/ActionExecutorTests.cs`
- Modify: `tests/AreWeDoomd.UnitTests/AgentService/Processing/AgentEventProcessorTests.cs` (compile fix only: `Mock<IActionExecutor>` setups/verifies now use the new return type — `ReturnsAsync(new ActionExecutionResult(ActionExecutionOutcome.Executed, null))` where a setup exists; `Verify` calls are unchanged in shape)

**Interfaces:**
- Consumes: `AgentDecision`, `AgentAction` (existing).
- Produces (Task 6 depends on this):

```csharp
public enum ActionExecutionOutcome { Executed = 0, Ignored = 1, Failed = 2 }

public sealed record ActionExecutionResult(ActionExecutionOutcome Outcome, string? ErrorDetail = null);

public interface IActionExecutor
{
    Task<ActionExecutionResult> ExecuteAsync(
        AgentDecision decision, Guid postId, Guid commentId, string actingUserId, CancellationToken ct);
}
```

Mapping inside `ActionExecutor.ExecuteAsync` (behavioral contract, existing log lines stay):
- `decision.Action == AgentAction.Ignore` → `Ignored`.
- HTTP success → `Executed`.
- HTTP non-success → `Failed`, `ErrorDetail = $"HTTP {(int)status}: {body}"` (body truncated to 500 chars).
- Transport exception (the existing catch) → `Failed`, `ErrorDetail = ex.Message`.
- `OperationCanceledException` when ct cancelled → still rethrows (unchanged).

- [ ] **Step 1: Write/extend the failing tests**

In `ActionExecutorTests.cs`, follow the file's existing HttpClient-mocking pattern (read it first; it exists and passes today). Add/adjust assertions so that:

```csharp
// existing happy-path test gains:
result.Outcome.ShouldBe(ActionExecutionOutcome.Executed);

// existing/new ignore test:
result.Outcome.ShouldBe(ActionExecutionOutcome.Ignored);

// new failure test (respond 500 with body "boom"):
result.Outcome.ShouldBe(ActionExecutionOutcome.Failed);
result.ErrorDetail.ShouldNotBeNull();
result.ErrorDetail.ShouldContain("500");
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/AreWeDoomd.UnitTests --filter "FullyQualifiedName~ActionExecutorTests"`
Expected: FAIL — compile error (void Task has no result).

- [ ] **Step 3: Implement**

Create the enum + record files (one type per file), change the interface, and update `ActionExecutor.ExecuteAsync` per the mapping contract: `return new ActionExecutionResult(ActionExecutionOutcome.Ignored);` in the Ignore branch; `return new ActionExecutionResult(ActionExecutionOutcome.Executed);` in the success branch; in the non-success branch build `ErrorDetail` from status + truncated body; in the transport catch return `Failed` with `ex.Message`. Fix `AgentEventProcessorTests` mock compile errors (mechanical).

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/AreWeDoomd.UnitTests`
Expected: all pass.

- [ ] **Step 5: Commit**

```powershell
git add -A; git commit -m "feat: ActionExecutor reports execution outcome"
```

---

### Task 6: Consolidated decision-log entries in AgentEventProcessor

**Files:**
- Create: `src/AreWeDoomd.AgentService/Decisions/LlmDecisionSource.cs`
- Create: `src/AreWeDoomd.AgentService/Decisions/LlmDecisionResult.cs`
- Modify: `src/AreWeDoomd.AgentService/Processing/AgentEventProcessor.cs`
- Modify: `src/AreWeDoomd.AgentService/Program.cs` (processor factory gains the writer argument)
- Test: `tests/AreWeDoomd.UnitTests/AgentService/Processing/AgentEventProcessorTests.cs`

**Interfaces:**
- Consumes: `IDecisionLogWriter` (Task 3), `string? IAiSessionLogger.Log` (Task 4), `ActionExecutionResult` (Task 5).
- Produces:

```csharp
public enum LlmDecisionSource { Parsed = 0, ProviderFailed = 1, InvalidJson = 2 }

public sealed record LlmDecisionResult(
    AgentDecision Decision, int Attempts, LlmDecisionSource Source, string? ErrorDetail, string? SessionLogRef);
```

**Behavioral contract — exactly ONE `TryLog` per event that reaches the pipeline, at these exits of `ProcessSingleAsync`:**

| Exit | Outcome | Notes |
|---|---|---|
| decay `EffectivePriority.Skip` | `SkippedPriority` | no LLM fields; `Priority = "Skip"` |
| LLM: both attempts provider-failed | `LlmFailed` | `ErrorDetail` = last provider error, `LlmAttempts = 2` |
| LLM: fallback after invalid JSON | `LlmFallback` | `LlmAttempts = 2` |
| decision executed OK | `Executed` | full fields |
| decision was Ignore (parsed) | `Ignored` | `Action = "ignore"`, reasoning kept |
| action HTTP/transport failure | `ActionFailed` | `ErrorDetail` from `ActionExecutionResult` |

Early exits BEFORE the pipeline (no AI recipient, non-CommentCreated type, unparseable ids, context-fetch null) do NOT log — per Global Constraints deviation (c).

Common entry fields at every logged exit: `Ts = DateTimeOffset.UtcNow`, `AiUserId = aiRecipient.UserId`, `ActivityId`, `ActivityType = agentEvent.ActivityType.ToString()`, `PostId`/`CommentId` (parsed values), `Priority = priority.ToString()` (where reached), `Action = JsonNamingPolicy.SnakeCaseLower.ConvertName(decision.Action.ToString())` (where a decision exists), `Reasoning = decision.Reasoning`, `Content = decision.Content`, `SessionLogRef`/`LlmAttempts` from `LlmDecisionResult`.

- [ ] **Step 1: Write the failing tests**

Extend `AgentEventProcessorTests` (constructor gains `private readonly Mock<IDecisionLogWriter> _decisionLog = new();`, passed to `CreateProcessor()`):

```csharp
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
    // SetupLlmResponses variant: provider returns ChatResult.Fail twice —
    // add a SetupLlmFailures() helper mirroring SetupLlmResponses.
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
```

(An invalid-JSON-twice → `LlmFallback` test follows the same shape with `SetupLlmResponses("not json", "not json")`. A `SkippedPriority` test is included only if a Skip scenario is constructible from `SampleEvent(ActorType.Ai)` + the existing `SampleContext` against the real `PriorityDecayPolicy` — check `PriorityDecayPolicyTests` for the minimal Skip input; if it needs an elaborate comment chain, cover the Skip mapping implicitly and note it in the report.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/AreWeDoomd.UnitTests --filter "FullyQualifiedName~AgentEventProcessorTests"`
Expected: FAIL — `CreateProcessor` doesn't accept the writer / entries never logged.

- [ ] **Step 3: Implement**

- `GetDecisionAsync` → returns `LlmDecisionResult`: track per-attempt session refs (keep the LAST non-null `_sessionLogger.Log(...)` return), attempts used, and classify the exit: parsed → `Parsed`; loop exhausted with last failure being `!result.IsSuccess` → `ProviderFailed` (ErrorDetail = last `result.Error?.Message`); exhausted via parse failures → `InvalidJson`. The fallback `AgentDecision(AgentAction.Ignore, null, "fallback: …")` stays as the `Decision` on both failure sources.
- `ProcessSingleAsync`: constructor takes `IDecisionLogWriter decisionLog` (after `sessionLogger`); build the common entry fields once; `TryLog` at each exit per the contract table. On the `ProviderFailed`/`InvalidJson` sources, log `LlmFailed`/`LlmFallback` and do NOT call `_actionExecutor` (behavior change from today, where the fallback Ignore decision was passed to the executor and produced a second "ignore" log line — the consolidated entry replaces that; the executor call was already a no-op for Ignore).
- `Program.cs`: add `serviceProvider.GetRequiredService<IDecisionLogWriter>()` to the `AgentEventProcessor` factory arguments.

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/AreWeDoomd.UnitTests`
Expected: all pass, including untouched older processor tests.

- [ ] **Step 5: Commit**

```powershell
git add -A; git commit -m "feat: write one consolidated decision-log entry per agent event"
```

---

### Task 7: Log dropped queue events in AgentNotificationListener

**Files:**
- Modify: `src/AreWeDoomd.AgentService/AgentNotificationListener.cs`
- Test: `tests/AreWeDoomd.UnitTests/AgentService/AgentNotificationListenerTests.cs` (create)

**Interfaces:**
- Consumes: `IDecisionLogWriter` (Task 3), `AgentEventQueue` (existing, concrete, capacity 100).
- Produces: `internal void HandleNotification(ActivityNotification notification)` on the listener — extracted from the `_connection.On<ActivityNotification>` callback so the drop path is unit-testable. The SignalR wiring calls it: `_connection.On<ActivityNotification>(AgentNotificationHubConstants.ReceiveEventMethod, HandleNotification);`.

Dropped-entry contract: `Outcome = Dropped`, `AiUserId` = first recipient with `RecipientType == NotificationRecipientType.Ai` (empty string if none), `ActivityId`, `ActivityType`, `Priority` = that recipient's `Priority.ToString()` (null if none). No decision fields.

- [ ] **Step 1: Write the failing test**

```csharp
using AreWeDoomd.ActivityNotifications.Contracts;
using AreWeDoomd.AgentService;
using AreWeDoomd.AgentService.Logging;
using AreWeDoomd.AgentService.Processing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace AreWeDoomd.UnitTests.AgentService;

public sealed class AgentNotificationListenerTests
{
    [Fact]
    public void HandleNotification_WhenQueueFull_ShouldLogDroppedEntry()
    {
        var queue = new AgentEventQueue();
        var decisionLog = new Mock<IDecisionLogWriter>();
        var listener = new AgentNotificationListener(
            Options.Create(new AgentServiceOptions()),
            queue,
            decisionLog.Object,
            NullLogger<AgentNotificationListener>.Instance);
        for (int i = 0; i < 100; i++) { queue.TryEnqueue(SampleAgentEvent($"fill-{i}")); }

        listener.HandleNotification(SampleNotification("overflow-1"));

        decisionLog.Verify(w => w.TryLog(It.Is<DecisionLogEntry>(e =>
            e.Outcome == DecisionOutcome.Dropped && e.ActivityId == "overflow-1")), Times.Once);
    }

    [Fact]
    public void HandleNotification_WhenQueueAccepts_ShouldNotLog()
    {
        var queue = new AgentEventQueue();
        var decisionLog = new Mock<IDecisionLogWriter>();
        var listener = new AgentNotificationListener(
            Options.Create(new AgentServiceOptions()), queue, decisionLog.Object,
            NullLogger<AgentNotificationListener>.Instance);

        listener.HandleNotification(SampleNotification("ok-1"));

        decisionLog.Verify(w => w.TryLog(It.IsAny<DecisionLogEntry>()), Times.Never);
    }

    // SampleNotification / SampleAgentEvent helpers: construct a minimal
    // ActivityNotification (CommentCreated, one Ai recipient) — copy the shape
    // from AgentEventProcessorTests.SampleEvent and ActivityNotification's ctor.
}
```

(NOTE for the queue-full setup: `AgentEventQueue` is `DropOldest` capacity 100 — `TryEnqueue` never returns false on overflow, it evicts. Check `AgentEventQueue.TryWrite` semantics FIRST: with `BoundedChannelFullMode.DropOldest`, `TryWrite` returns true and evicts the oldest. **This means the listener's `else` branch is currently dead code** and the drop actually happens silently inside the channel. THE FIX IS PART OF THIS TASK: change `AgentEventQueue`'s `FullMode` to `BoundedChannelFullMode.DropWrite` so `TryEnqueue` genuinely returns false when full and the listener's reject branch — now with the decision-log write — becomes real. Same capacity 100. This makes drops visible instead of silent eviction, which is the entire point of the `dropped` outcome. Add file `Processing/AgentEventQueue.cs` to the Modify list and mention the semantic change prominently in the implementer report.)

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AreWeDoomd.UnitTests --filter "FullyQualifiedName~AgentNotificationListenerTests"`
Expected: FAIL — constructor has no `IDecisionLogWriter` parameter / `HandleNotification` doesn't exist.

- [ ] **Step 3: Implement**

- `AgentEventQueue`: `FullMode = BoundedChannelFullMode.DropWrite` (comment: "DropWrite so a full queue rejects visibly — the listener logs a dropped decision entry — instead of silently evicting the oldest event").
- `AgentNotificationListener`: constructor gains `IDecisionLogWriter decisionLog` (third parameter, before logger — update `Program.cs`? No: the listener is registered via `AddHostedService<AgentNotificationListener>()`, DI resolves the new parameter automatically). Extract the `On` callback body into `internal void HandleNotification(ActivityNotification notification)`; in the reject branch, alongside the existing `LogWarning`, build and `TryLog` the dropped entry per the contract above.
- `AreWeDoomd.UnitTests` needs `InternalsVisibleTo`? No — check first: if `AreWeDoomd.AgentService.csproj` has no `InternalsVisibleTo`, add to it:

```xml
  <ItemGroup>
    <InternalsVisibleTo Include="AreWeDoomd.UnitTests" />
  </ItemGroup>
```

(unless the codebase already exposes internals another way — mirror whatever `AreWeDoomd.Application` does for its tests, if anything; otherwise make `HandleNotification` `public` and skip InternalsVisibleTo — prefer whichever the existing codebase convention is.)

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/AreWeDoomd.UnitTests`
Expected: all pass.

- [ ] **Step 5: Commit**

```powershell
git add -A; git commit -m "feat: log dropped agent events and make queue overflow visible"
```

---

### Task 8: Compose volume + final verification

**Files:**
- Modify: `docker-compose.yml`
- No test files — verification task.

**Interfaces:**
- Consumes: everything above.
- Produces: shared `agent-logs` named volume readable (ro) by the api container — the M3 reader's contract: api sees decisions at `/app/agent-logs/agent-decisions`, sessions at `/app/agent-logs/ai-sessions`.

- [ ] **Step 1: Edit docker-compose.yml**

`agent-service` service — add:

```yaml
    environment:
      # (existing entries stay)
      # Decision + session logs land on the shared volume (mounted at /app/logs).
      DECISION_LOG_ROOT: /app/logs/agent-decisions
    volumes:
      - agent-logs:/app/logs
```

`api` service — add (consumed by the Api starting in M3; declared now so the volume contract is fixed):

```yaml
    environment:
      # (existing entries stay)
      DECISION_LOG_ROOT: /app/agent-logs/agent-decisions
    volumes:
      - agent-logs:/app/agent-logs:ro
```

Top-level `volumes:` — add `agent-logs:` alongside `redis-data:`.

(Why this works without more code: `AiSessionLogger` writes relative `logs/ai-sessions` and `DecisionLogOptions.RootPath` defaults relative `logs/agent-decisions`; the AgentService container's workdir is `/app`, so both land inside the mounted volume. The explicit `DECISION_LOG_ROOT` just pins it.)

- [ ] **Step 2: Full verification**

Run: `dotnet build --no-incremental; dotnet test`
Expected: solution builds, ALL tests pass (both test projects).

Run: `git grep -n "DecisionLog" src/AreWeDoomd.AgentService/Program.cs`
Expected: env mapping + Configure + three registrations present.

Optional (docker available): `docker compose config` from the repo root — validates YAML; expected: renders with `agent-logs` volume on both services.

- [ ] **Step 3: Manual smoke note for the controller**

Run the API + AgentService locally (or compose), post a comment on an AI user's post, then check `logs/agent-decisions/decisions-{today}.jsonl` contains one line with `"outcome":"executed"` (or `ignored`) and a populated `reasoning` + `sessionLogRef`. This is the M1 exit criterion from the spec.

- [ ] **Step 4: Commit**

```powershell
git add -A; git commit -m "feat: share agent decision/session logs via compose volume"
```

---

## Self-Review Notes

- **Spec §3.1 coverage:** JSONL daily UTC files ✓; one consolidated line per event via ActionExecutor outcome ✓; all 7 outcomes emitted (`llm_failed`/`llm_fallback` split by failure source; `dropped` from the listener) ✓; non-blocking bounded channel, write failure never stalls processing ✓; `FileShare.Read` append ✓; writer-only retention 30d on startup + daily ✓; `DECISION_LOG_ROOT` env, no committed absolute paths ✓; compose volume rw/ro ✓; `sessionLogRef` via Task 4 ✓; `personaVersion`/`personaSource` fields reserved null for M2 ✓.
- **Deliberate deviations** (also listed in Global Constraints): no `aiUsername`; `DropWrite` instead of `DropOldest` in BOTH channels (decision-log channel and — flagged loudly in Task 7 — the existing `AgentEventQueue`, whose current DropOldest makes the listener's reject branch dead code; this is a real behavior change: under overflow we now keep old events and reject new ones, logged); pre-decision exits unlogged in M1.
- **Type consistency check:** `DecisionLogEntry` field set matches every `TryLog` call site; `ActionExecutionResult`/`LlmDecisionResult` names consistent across Tasks 5–6; `IDecisionLogWriter.TryLog` signature identical in Tasks 3, 6, 7.
- **M0 interaction:** only Task 4 touches files whose `using` lines M0 rewrites (`IAiSessionLogger`/`AiSessionLogger` gain `using AreWeDoomd.ChatProviders;` in M0 Task 2). Execute M1 after M0 merges — or if M1 starts first, the task text already says "use whichever namespace the current branch compiles with."
