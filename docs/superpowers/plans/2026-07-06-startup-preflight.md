# Startup Preflight & Package Security Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the Microsoft.OpenApi vulnerability, add a blocking database preflight with configurable retry delays for Azure SQL serverless cold start, and log ChatProvider API key status at startup instead of throwing.

**Architecture:** A direct NuGet reference overrides the vulnerable transitive package. A static `DatabasePreflight` helper in the Api layer blocks startup (before admin seeding) while probing the DB on a config-driven retry schedule, then lets the app start regardless; a `/health` endpoint reports live DB state. A shared `ChatProviderStartupSummary` helper in the ChatProviders project logs per-provider key presence (Warning per missing, Error if none / if the selected provider is missing) and is used by both the API and the AgentService; the AgentService switches to deferred validation and skips registering its LLM pipeline hosted services when the selected provider has no key.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core (SQL Server), Serilog, xUnit + Moq + Shouldly.

**Repo root for all commands and paths:** `C:\Users\Doga\source\repos\IMPORTRADER\AreWeDoomd\AreWeDoomd.Api` (a git repo, branch `dev`).

## Global Constraints

- One class per `.cs` file (AGENTS.md).
- Braces always required, even for single-line control flow (AGENTS.md).
- Private fields `_camelCase`, interfaces `I` prefix, PascalCase types/methods (AGENTS.md).
- Test naming: `MethodName_StateUnderTest_ExpectedBehaviour`; stack is xUnit + Moq + Shouldly only (docs/ai/unit-test-rules.md).
- Clean Architecture direction: API → Application → Domain; Infrastructure → Application/Domain. Startup concerns live in the Api project.
- Git commits must NOT contain a `Co-Authored-By` trailer (user global rule).
- `docs/superpowers/` is gitignored — never force-add it.
- The build may emit NU1903 warnings until Task 1 lands; treat only errors as build failures.

---

### Task 1: Override vulnerable Microsoft.OpenApi package

The solution's only vulnerability: `Microsoft.OpenApi 2.0.0` (High, GHSA-v5pm-xwqc-g5wc), pulled transitively by `Microsoft.AspNetCore.OpenApi 10.0.9`. NuGet's "direct dependency wins" rule fixes it: add a direct reference to the patched 2.x version. License is MIT; stay on the 2.x line (ASP.NET Core 10 targets the 2.x API — do not use 3.x).

**Files:**
- Modify: `src/AreWeDoomd.Api/AreWeDoomd.Api.csproj`

**Interfaces:**
- Consumes: nothing.
- Produces: nothing code-visible; downstream tasks build on a warning-free solution.

- [ ] **Step 1: Add the direct package reference**

In `src/AreWeDoomd.Api/AreWeDoomd.Api.csproj`, inside the existing first `<ItemGroup>` of `<PackageReference>` items, add (alphabetical position — after `Microsoft.EntityFrameworkCore.Design`):

```xml
    <PackageReference Include="Microsoft.OpenApi" Version="2.9.0" />
```

- [ ] **Step 2: Restore and verify the vulnerability is gone**

Run (from repo root):

```powershell
dotnet restore
dotnet list package --vulnerable --include-transitive
```

Expected: every project reports "no vulnerable packages"; no NU1903 warnings. If `2.9.0` cannot be resolved from the configured feeds, fall back to `2.7.6` (also patched) and re-verify.

- [ ] **Step 3: Build the solution**

```powershell
dotnet build
```

Expected: Build succeeded, no NU1903 warnings.

- [ ] **Step 4: Commit**

```powershell
git add src/AreWeDoomd.Api/AreWeDoomd.Api.csproj
git commit -m "fix: override transitive Microsoft.OpenApi 2.0.0 with patched 2.9.0 (GHSA-v5pm-xwqc-g5wc)"
```

---

### Task 2: ChatProviderStartupSummary helper (shared, TDD)

A static helper in the ChatProviders project that reports which providers have API keys. Used by both hosts in later tasks.

**Files:**
- Create: `src/AreWeDoomd.ChatProviders/ChatProviderStartupSummary.cs`
- Test: `tests/AreWeDoomd.UnitTests/ChatProviders/ChatProviderStartupSummaryTests.cs`
- Test helper: `tests/AreWeDoomd.UnitTests/ChatProviders/CapturingLogger.cs`

**Interfaces:**
- Consumes: existing constants `GeminiProvider.ProviderName` (= `"gemini"`), `OpenRouterProvider.ProviderName` (= `"openrouter"`), `GeminiProviderOptions.SectionName` (= `"ChatProviders:Gemini"`), `OpenRouterProviderOptions.SectionName` (= `"ChatProviders:OpenRouter"`).
- Produces (used by Tasks 4 and 5):
  - `public static bool ChatProviderStartupSummary.IsProviderConfigured(IConfiguration configuration, string providerName)`
  - `public static bool ChatProviderStartupSummary.LogSummary(IConfiguration configuration, string selectedProvider, ILogger logger)` — returns whether the *selected* provider has a key.

- [ ] **Step 1: Write the failing tests**

Create `tests/AreWeDoomd.UnitTests/ChatProviders/CapturingLogger.cs` — a hand-rolled `ILogger` that records log level + rendered message (Moq is awkward for `ILogger.Log<TState>`):

```csharp
using Microsoft.Extensions.Logging;

namespace AreWeDoomd.UnitTests.ChatProviders;

public sealed class CapturingLogger : ILogger
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add((logLevel, formatter(state, exception)));
    }
}
```

Create `tests/AreWeDoomd.UnitTests/ChatProviders/ChatProviderStartupSummaryTests.cs`:

```csharp
using AreWeDoomd.ChatProviders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace AreWeDoomd.UnitTests.ChatProviders;

public class ChatProviderStartupSummaryTests
{
    private static IConfiguration BuildConfiguration(string? geminiKey, string? openRouterKey)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ChatProviders:Gemini:ApiKey"] = geminiKey,
                ["ChatProviders:OpenRouter:ApiKey"] = openRouterKey
            })
            .Build();
    }

    [Fact]
    public void LogSummary_WhenAllProvidersHaveKeys_ShouldLogNoWarningsOrErrors()
    {
        var configuration = BuildConfiguration("gk", "ok");
        var logger = new CapturingLogger();

        var result = ChatProviderStartupSummary.LogSummary(configuration, "gemini", logger);

        result.ShouldBeTrue();
        logger.Entries.ShouldNotContain(e => e.Level == LogLevel.Warning);
        logger.Entries.ShouldNotContain(e => e.Level == LogLevel.Error);
    }

    [Fact]
    public void LogSummary_WhenOneProviderMissingKey_ShouldLogWarningForThatProvider()
    {
        var configuration = BuildConfiguration("gk", null);
        var logger = new CapturingLogger();

        var result = ChatProviderStartupSummary.LogSummary(configuration, "gemini", logger);

        result.ShouldBeTrue();
        logger.Entries.Count(e => e.Level == LogLevel.Warning).ShouldBe(1);
        logger.Entries.Single(e => e.Level == LogLevel.Warning).Message.ShouldContain("openrouter");
        logger.Entries.ShouldNotContain(e => e.Level == LogLevel.Error);
    }

    [Fact]
    public void LogSummary_WhenNoProviderHasKey_ShouldLogError()
    {
        var configuration = BuildConfiguration(null, null);
        var logger = new CapturingLogger();

        var result = ChatProviderStartupSummary.LogSummary(configuration, "gemini", logger);

        result.ShouldBeFalse();
        logger.Entries.Count(e => e.Level == LogLevel.Warning).ShouldBe(2);
        logger.Entries.ShouldContain(e => e.Level == LogLevel.Error);
    }

    [Fact]
    public void LogSummary_WhenSelectedProviderMissingKey_ShouldReturnFalseAndLogError()
    {
        var configuration = BuildConfiguration("gk", null);
        var logger = new CapturingLogger();

        var result = ChatProviderStartupSummary.LogSummary(configuration, "openrouter", logger);

        result.ShouldBeFalse();
        logger.Entries.ShouldContain(e => e.Level == LogLevel.Error && e.Message.Contains("openrouter"));
    }

    [Fact]
    public void IsProviderConfigured_WhenKeyPresent_ShouldReturnTrue()
    {
        var configuration = BuildConfiguration("gk", null);

        ChatProviderStartupSummary.IsProviderConfigured(configuration, "gemini").ShouldBeTrue();
    }

    [Fact]
    public void IsProviderConfigured_WhenKeyMissingOrProviderUnknown_ShouldReturnFalse()
    {
        var configuration = BuildConfiguration("gk", null);

        ChatProviderStartupSummary.IsProviderConfigured(configuration, "openrouter").ShouldBeFalse();
        ChatProviderStartupSummary.IsProviderConfigured(configuration, "no-such-provider").ShouldBeFalse();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```powershell
dotnet test tests/AreWeDoomd.UnitTests --filter "FullyQualifiedName~ChatProviderStartupSummary"
```

Expected: FAIL to compile — `ChatProviderStartupSummary` does not exist.

- [ ] **Step 3: Implement the helper**

Create `src/AreWeDoomd.ChatProviders/ChatProviderStartupSummary.cs`:

```csharp
using AreWeDoomd.ChatProviders.Providers.Gemini;
using AreWeDoomd.ChatProviders.Providers.OpenRouter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AreWeDoomd.ChatProviders;

/// <summary>
/// Logs which registered chat providers have an API key configured, so a missing
/// key surfaces as a startup log line instead of a runtime exception.
/// </summary>
public static class ChatProviderStartupSummary
{
    private static readonly IReadOnlyDictionary<string, string> ProviderSections =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [GeminiProvider.ProviderName] = GeminiProviderOptions.SectionName,
            [OpenRouterProvider.ProviderName] = OpenRouterProviderOptions.SectionName
        };

    public static bool IsProviderConfigured(IConfiguration configuration, string providerName)
    {
        return ProviderSections.TryGetValue(providerName, out var section)
            && !string.IsNullOrWhiteSpace(configuration[$"{section}:ApiKey"]);
    }

    /// <summary>
    /// Logs one line per registered provider (Information when its key is present,
    /// Warning when missing), an Error when no provider has a key, and an Error when
    /// the selected provider has no key. Returns whether the selected provider is usable.
    /// </summary>
    public static bool LogSummary(IConfiguration configuration, string selectedProvider, ILogger logger)
    {
        var configuredCount = 0;

        foreach (var (providerName, section) in ProviderSections)
        {
            if (!string.IsNullOrWhiteSpace(configuration[$"{section}:ApiKey"]))
            {
                configuredCount++;
                logger.LogInformation("Chat provider '{Provider}' has an API key configured.", providerName);
            }
            else
            {
                logger.LogWarning("Chat provider '{Provider}' has no API key configured.", providerName);
            }
        }

        if (configuredCount == 0)
        {
            logger.LogError("No chat provider has an API key configured; LLM-dependent features will not work.");
        }

        var selectedConfigured = IsProviderConfigured(configuration, selectedProvider);

        if (selectedConfigured)
        {
            logger.LogInformation("Selected chat provider: '{Provider}'.", selectedProvider);
        }
        else
        {
            logger.LogError(
                "Selected chat provider '{Provider}' has no API key configured (or is not a registered provider).",
                selectedProvider);
        }

        return selectedConfigured;
    }
}
```

Note: the ChatProviders project already references `Microsoft.Extensions.Configuration` and logging abstractions (providers take `ILogger`). If `Microsoft.Extensions.Logging.Abstractions` is somehow missing from `src/AreWeDoomd.ChatProviders/AreWeDoomd.ChatProviders.csproj`, add `<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />`.

- [ ] **Step 4: Run the tests to verify they pass**

```powershell
dotnet test tests/AreWeDoomd.UnitTests --filter "FullyQualifiedName~ChatProviderStartupSummary"
```

Expected: 6 tests PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/AreWeDoomd.ChatProviders/ChatProviderStartupSummary.cs tests/AreWeDoomd.UnitTests/ChatProviders/
git commit -m "feat: add chat provider API key startup summary helper"
```

---

### Task 3: DatabasePreflight core logic (TDD)

A static helper in the Api project. The retry loop is a pure, testable method taking injected probe and delay functions; the `WebApplication` wrapper wires the real DbContext probe.

**Files:**
- Create: `src/AreWeDoomd.Api/Startup/DatabasePreflightOptions.cs`
- Create: `src/AreWeDoomd.Api/Startup/DatabasePreflight.cs`
- Test: `tests/AreWeDoomd.UnitTests/Api/Startup/DatabasePreflightTests.cs`

**Interfaces:**
- Consumes: `AreWeDoomdDbContext` (namespace `AreWeDoomd.Infrastructure.Common.Persistence`), `CapturingLogger` from Task 2 (namespace `AreWeDoomd.UnitTests.ChatProviders`).
- Produces (used by Task 4):
  - `public sealed class DatabasePreflightOptions` with `const string SectionName = "Preflight:Database"`, `bool Enabled { get; set; } = true`, `List<TimeSpan> RetryDelays { get; set; }`
  - `public static Task<bool> DatabasePreflight.RunAsync(WebApplication app)`
  - `public static Task<bool> DatabasePreflight.ExecuteAsync(Func<CancellationToken, Task<bool>> probeAsync, IReadOnlyList<TimeSpan> retryDelays, Func<TimeSpan, CancellationToken, Task> delayAsync, ILogger logger, CancellationToken cancellationToken = default)`

- [ ] **Step 1: Write the failing tests**

Create `tests/AreWeDoomd.UnitTests/Api/Startup/DatabasePreflightTests.cs`:

```csharp
using AreWeDoomd.Api.Startup;
using AreWeDoomd.UnitTests.ChatProviders;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace AreWeDoomd.UnitTests.Api.Startup;

public class DatabasePreflightTests
{
    private static readonly IReadOnlyList<TimeSpan> TwoDelays =
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)];

    [Fact]
    public async Task ExecuteAsync_WhenFirstAttemptSucceeds_ShouldReturnTrueWithoutDelaying()
    {
        var delays = new List<TimeSpan>();
        var logger = new CapturingLogger();

        var result = await DatabasePreflight.ExecuteAsync(
            _ => Task.FromResult(true),
            TwoDelays,
            (delay, _) => { delays.Add(delay); return Task.CompletedTask; },
            logger);

        result.ShouldBeTrue();
        delays.ShouldBeEmpty();
        logger.Entries.ShouldContain(e => e.Level == LogLevel.Information);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSecondAttemptSucceeds_ShouldWaitFirstDelayAndLogWarning()
    {
        var delays = new List<TimeSpan>();
        var logger = new CapturingLogger();
        var attempts = 0;

        var result = await DatabasePreflight.ExecuteAsync(
            _ => Task.FromResult(++attempts >= 2),
            TwoDelays,
            (delay, _) => { delays.Add(delay); return Task.CompletedTask; },
            logger);

        result.ShouldBeTrue();
        delays.ShouldBe(new[] { TimeSpan.FromSeconds(1) });
        logger.Entries.Count(e => e.Level == LogLevel.Warning).ShouldBe(1);
        logger.Entries.ShouldNotContain(e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAllAttemptsFail_ShouldUseEveryDelayThenReturnFalseAndLogError()
    {
        var delays = new List<TimeSpan>();
        var logger = new CapturingLogger();

        var result = await DatabasePreflight.ExecuteAsync(
            _ => Task.FromResult(false),
            TwoDelays,
            (delay, _) => { delays.Add(delay); return Task.CompletedTask; },
            logger);

        result.ShouldBeFalse();
        delays.ShouldBe(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) });
        logger.Entries.Count(e => e.Level == LogLevel.Warning).ShouldBe(2);
        logger.Entries.Count(e => e.Level == LogLevel.Error).ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenProbeThrows_ShouldTreatAsFailedAttemptAndRetry()
    {
        var logger = new CapturingLogger();
        var attempts = 0;

        var result = await DatabasePreflight.ExecuteAsync(
            _ =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new InvalidOperationException("db is waking up");
                }
                return Task.FromResult(true);
            },
            TwoDelays,
            (_, _) => Task.CompletedTask,
            logger);

        result.ShouldBeTrue();
        attempts.ShouldBe(2);
        logger.Entries.Single(e => e.Level == LogLevel.Warning).Message.ShouldContain("db is waking up");
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoDelaysConfigured_ShouldTryExactlyOnce()
    {
        var logger = new CapturingLogger();
        var attempts = 0;

        var result = await DatabasePreflight.ExecuteAsync(
            _ => { attempts++; return Task.FromResult(false); },
            Array.Empty<TimeSpan>(),
            (_, _) => Task.CompletedTask,
            logger);

        result.ShouldBeFalse();
        attempts.ShouldBe(1);
        logger.Entries.Count(e => e.Level == LogLevel.Error).ShouldBe(1);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```powershell
dotnet test tests/AreWeDoomd.UnitTests --filter "FullyQualifiedName~DatabasePreflight"
```

Expected: FAIL to compile — `DatabasePreflight` does not exist.

- [ ] **Step 3: Implement options and preflight**

Create `src/AreWeDoomd.Api/Startup/DatabasePreflightOptions.cs`:

```csharp
namespace AreWeDoomd.Api.Startup;

public sealed class DatabasePreflightOptions
{
    public const string SectionName = "Preflight:Database";

    /// <summary>Integration tests disable the preflight the same way they disable admin seeding.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Waits between attempts; total attempts = RetryDelays.Count + 1.</summary>
    public List<TimeSpan> RetryDelays { get; set; } = [];
}
```

Create `src/AreWeDoomd.Api/Startup/DatabasePreflight.cs`:

```csharp
using AreWeDoomd.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AreWeDoomd.Api.Startup;

/// <summary>
/// Blocks startup while probing the database on a configured retry schedule, so an
/// Azure SQL serverless cold start (~1.5 min resume) does not take the API down.
/// Each probe also nudges the paused database to resume. If every attempt fails the
/// API still starts; /health keeps reporting the live database state.
/// </summary>
public static class DatabasePreflight
{
    public static async Task<bool> RunAsync(WebApplication app)
    {
        var options = app.Configuration
            .GetSection(DatabasePreflightOptions.SectionName)
            .Get<DatabasePreflightOptions>() ?? new DatabasePreflightOptions();

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabasePreflight");

        if (!options.Enabled)
        {
            logger.LogInformation("Database preflight is disabled by configuration.");
            return true;
        }

        return await ExecuteAsync(
            async cancellationToken =>
            {
                await using var scope = app.Services.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AreWeDoomdDbContext>();
                return await dbContext.Database.CanConnectAsync(cancellationToken);
            },
            options.RetryDelays,
            Task.Delay,
            logger,
            app.Lifetime.ApplicationStopping);
    }

    public static async Task<bool> ExecuteAsync(
        Func<CancellationToken, Task<bool>> probeAsync,
        IReadOnlyList<TimeSpan> retryDelays,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var totalAttempts = retryDelays.Count + 1;

        for (var attempt = 1; attempt <= totalAttempts; attempt++)
        {
            var succeeded = false;
            string failureReason = "connection probe returned false";

            try
            {
                succeeded = await probeAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failureReason = ex.Message;
            }

            if (succeeded)
            {
                logger.LogInformation(
                    "Database preflight succeeded on attempt {Attempt}/{TotalAttempts}.",
                    attempt, totalAttempts);
                return true;
            }

            if (attempt < totalAttempts)
            {
                var delay = retryDelays[attempt - 1];
                logger.LogWarning(
                    "Database preflight attempt {Attempt}/{TotalAttempts} failed ({Reason}); retrying in {Delay}.",
                    attempt, totalAttempts, failureReason, delay);
                await delayAsync(delay, cancellationToken);
            }
            else
            {
                logger.LogWarning(
                    "Database preflight attempt {Attempt}/{TotalAttempts} failed ({Reason}).",
                    attempt, totalAttempts, failureReason);
            }
        }

        logger.LogError(
            "Database preflight failed after {TotalAttempts} attempts; giving up. " +
            "The API will start, but database-dependent requests will fail until the database is reachable.",
            totalAttempts);
        return false;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```powershell
dotnet test tests/AreWeDoomd.UnitTests --filter "FullyQualifiedName~DatabasePreflight"
```

Expected: 5 tests PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/AreWeDoomd.Api/Startup/ tests/AreWeDoomd.UnitTests/Api/Startup/
git commit -m "feat: add database preflight retry logic for serverless cold start"
```

---

### Task 4: Wire preflight, health checks, and provider summary into the API

**Files:**
- Modify: `src/AreWeDoomd.Api/AreWeDoomd.Api.csproj` (health-check package)
- Modify: `src/AreWeDoomd.Api/Program.cs`
- Modify: `src/AreWeDoomd.Api/appsettings.json`
- Modify: `tests/AreWeDoomd.IntegrationTests/Realtime/AgentHubTestFactory.cs`
- Modify: `tests/AreWeDoomd.IntegrationTests/AgentService/StubbedApiFactory.cs`

**Interfaces:**
- Consumes: `DatabasePreflight.RunAsync(WebApplication)` (Task 3), `ChatProviderStartupSummary.LogSummary(IConfiguration, string, ILogger)` (Task 2), `PersonaGenerationOptions` (namespace `AreWeDoomd.Infrastructure.Common.Options`, `SectionName = "PersonaGeneration"`, `Provider` defaults to `"openrouter"`), `AreWeDoomdDbContext` (namespace `AreWeDoomd.Infrastructure.Common.Persistence`).
- Produces: `/health` endpoint; `Preflight:Database` config section.

- [ ] **Step 1: Add the EF health-check package**

In `src/AreWeDoomd.Api/AreWeDoomd.Api.csproj`, add to the same `<ItemGroup>` as Task 1:

```xml
    <PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" Version="10.0.9" />
```

Run `dotnet restore`. If `10.0.9` does not exist on the feed, use the latest `10.0.x` listed by `dotnet package search Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore --exact-match --source https://api.nuget.org/v3/index.json`.

- [ ] **Step 2: Add the preflight config section**

In `src/AreWeDoomd.Api/appsettings.json`, add a top-level section (e.g. after `"AllowedHosts": "*",`):

```json
  "Preflight": {
    "Database": {
      "Enabled": true,
      "RetryDelays": [
        "00:00:15",
        "00:00:15",
        "00:00:15",
        "00:00:15",
        "00:00:30",
        "00:00:30",
        "00:01:00",
        "00:02:00"
      ]
    }
  },
```

(9 attempts total, ~5 minutes. The slower series the user originally proposed — 30s/1m/2m/5m/10m — is a config-only change.)

- [ ] **Step 3: Wire into Program.cs**

In `src/AreWeDoomd.Api/Program.cs`:

a. Add usings at the top (with the existing usings):

```csharp
using AreWeDoomd.Api.Startup;
using AreWeDoomd.ChatProviders;
using AreWeDoomd.Infrastructure.Common.Options;
using AreWeDoomd.Infrastructure.Common.Persistence;
```

b. After `builder.Services.AddInfrastructure(builder.Configuration);` (currently line 101), register health checks:

```csharp
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<AreWeDoomdDbContext>();
```

c. Immediately after `var app = builder.Build();` (currently line 172) — and critically **before** `AdminSeeder.SeedAsync(app)`, which queries the database and would otherwise hit the cold start first — add:

```csharp
    await DatabasePreflight.RunAsync(app);

    var personaProvider = app.Configuration
        .GetSection(PersonaGenerationOptions.SectionName)
        .Get<PersonaGenerationOptions>()?.Provider ?? new PersonaGenerationOptions().Provider;
    ChatProviderStartupSummary.LogSummary(
        app.Configuration,
        personaProvider,
        app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ChatProviderStartup"));
```

(The `LogSummary` return value is intentionally ignored here — the API stays fully functional without LLM keys; persona generation already degrades via its `isConfigured` flag.)

d. After `app.MapControllers();` (currently line 234), map the health endpoint:

```csharp
    app.MapHealthChecks("/health");
```

- [ ] **Step 4: Disable preflight in the integration test factories**

Integration tests boot the real `Program` via `WebApplicationFactory<Program>`; without this they would probe the DB on startup. In **both** `tests/AreWeDoomd.IntegrationTests/Realtime/AgentHubTestFactory.cs` and `tests/AreWeDoomd.IntegrationTests/AgentService/StubbedApiFactory.cs`, next to the existing `builder.UseSetting("Admin:SeedOnStartup", "false");` line, add:

```csharp
        builder.UseSetting("Preflight:Database:Enabled", "false");
```

If any other `WebApplicationFactory<Program>` subclass exists (search: `grep -r "WebApplicationFactory<Program>" tests/`), add the same line there.

- [ ] **Step 5: Build and run the full test suite**

```powershell
dotnet build
dotnet test
```

Expected: build succeeds; all tests pass (integration tests unaffected thanks to the `Enabled=false` setting).

- [ ] **Step 6: Manual smoke check (optional but recommended)**

Run the API and watch the logs:

```powershell
dotnet run --project src/AreWeDoomd.Api
```

Expected log lines near startup: either `Database preflight succeeded on attempt 1/9` (DB awake) or `Database preflight attempt 1/9 failed (...); retrying in 00:00:15` (DB cold) — followed by the chat-provider summary lines. Then `GET http://localhost:5188/health` returns `Healthy`. Stop the app afterwards.

- [ ] **Step 7: Commit**

```powershell
git add src/AreWeDoomd.Api/ tests/AreWeDoomd.IntegrationTests/
git commit -m "feat: add blocking database preflight, /health endpoint, and chat provider startup summary to API"
```

---

### Task 5: AgentService — deferred validation and guarded pipeline start

Switch the AgentService off `ValidateOnStart` (which currently crashes the process when a key is missing) and instead: log the summary, and only register the LLM-dependent hosted services when the selected provider has a key. The process stays alive either way.

**Files:**
- Modify: `src/AreWeDoomd.AgentService/Program.cs`

**Interfaces:**
- Consumes: `ChatProviderStartupSummary.IsProviderConfigured` / `.LogSummary` (Task 2), `AgentServiceOptions` (`SectionName = "AgentNotifications"`, `ChatProvider` defaults to `"gemini"`).
- Produces: nothing new.

- [ ] **Step 1: Defer options validation**

In `src/AreWeDoomd.AgentService/Program.cs`, change line 76:

```csharp
builder.Services.AddChatProviders(builder.Configuration);
```

to:

```csharp
builder.Services.AddChatProviders(builder.Configuration, validateOnStart: false);
```

- [ ] **Step 2: Guard the LLM pipeline hosted services**

Still in `Program.cs`: the three hosted services that drive the LLM pipeline are `AgentNotificationListener`, `DailySchedulePlanner` (resolves the keyed `IChatProvider` internally), and the `AgentEventProcessor` factory registration (lines 104-124). Compute the provider status before `builder.Build()` and wrap all three registrations:

Replace:

```csharp
builder.Services.AddHostedService<AgentNotificationListener>();
builder.Services.AddHostedService<DailySchedulePlanner>();
builder.Services.AddHostedService(serviceProvider =>
{
    var agentOptions = serviceProvider.GetRequiredService<IOptions<AgentServiceOptions>>();
    return new AgentEventProcessor(
        // ... (existing factory body unchanged)
        serviceProvider.GetRequiredService<ILogger<AgentEventProcessor>>());
});
```

with:

```csharp
var selectedChatProvider = builder.Configuration
    .GetSection(AgentServiceOptions.SectionName)
    .Get<AgentServiceOptions>()?.ChatProvider ?? new AgentServiceOptions().ChatProvider;
var chatProviderConfigured =
    ChatProviderStartupSummary.IsProviderConfigured(builder.Configuration, selectedChatProvider);

if (chatProviderConfigured)
{
    builder.Services.AddHostedService<AgentNotificationListener>();
    builder.Services.AddHostedService<DailySchedulePlanner>();
    builder.Services.AddHostedService(serviceProvider =>
    {
        var agentOptions = serviceProvider.GetRequiredService<IOptions<AgentServiceOptions>>();
        return new AgentEventProcessor(
            serviceProvider.GetRequiredService<AgentEventQueue>(),
            serviceProvider.GetRequiredService<IContextFetcher>(),
            serviceProvider.GetRequiredService<PriorityDecayPolicy>(),
            serviceProvider.GetRequiredService<IPersonaProvider>(),
            serviceProvider.GetRequiredService<IPromptComposer>(),
            serviceProvider.GetRequiredKeyedService<IChatProvider>(agentOptions.Value.ChatProvider),
            serviceProvider.GetRequiredService<DecisionParser>(),
            serviceProvider.GetRequiredService<IActionExecutor>(),
            serviceProvider.GetRequiredService<IAiSessionLogger>(),
            serviceProvider.GetRequiredService<IDecisionLogWriter>(),
            serviceProvider.GetRequiredService<IAgentOpsLogWriter>(),
            serviceProvider.GetRequiredService<ILlmSettingsProvider>(),
            agentOptions,
            serviceProvider.GetRequiredService<ILogger<AgentEventProcessor>>());
    });
}
```

(The existing `AgentEventProcessor` factory body is copied verbatim — only the `if` wrapper and the two variables above it are new. The log writers `DecisionLogWriter` / `AgentOpsLogWriter` stay registered unconditionally; they are harmless without the pipeline.)

- [ ] **Step 3: Log the summary after build**

Replace the final two lines:

```csharp
var host = builder.Build();
host.Run();
```

with:

```csharp
var host = builder.Build();

ChatProviderStartupSummary.LogSummary(
    builder.Configuration,
    selectedChatProvider,
    host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ChatProviderStartup"));

if (!chatProviderConfigured)
{
    host.Services.GetRequiredService<ILoggerFactory>()
        .CreateLogger("ChatProviderStartup")
        .LogError(
            "Agent pipeline not started: selected chat provider '{Provider}' has no API key. " +
            "Set the key and restart the service.",
            selectedChatProvider);
}

host.Run();
```

Note: `LogError` needs `using Microsoft.Extensions.Logging;` — already present at the top of the file.

- [ ] **Step 4: Build and verify both behaviors**

```powershell
dotnet build
```

Expected: build succeeds.

Then verify the no-key path does not crash. From the repo root, in an environment where `GEMINI_API_KEY` / `OPENROUTER_API_KEY` are unset and appsettings keys are empty:

```powershell
dotnet run --project src/AreWeDoomd.AgentService
```

Expected: process starts and stays alive; logs contain two Warnings (one per provider), the "No chat provider has an API key configured" Error, the "Selected chat provider 'openrouter' has no API key" Error, and the "Agent pipeline not started" Error — and **no unhandled exception**. Stop it with Ctrl+C. (Note: `AgentServiceOptions.ChatProvider` defaults to `"gemini"` in code, but appsettings.json sets `"AgentNotifications:ChatProvider": "openrouter"`.)

- [ ] **Step 5: Run the full test suite**

```powershell
dotnet test
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src/AreWeDoomd.AgentService/Program.cs
git commit -m "feat: log chat provider key status at AgentService startup instead of crashing"
```

---

## Final verification (after all tasks)

```powershell
dotnet list package --vulnerable --include-transitive   # expect: clean
dotnet build                                             # expect: no warnings about NU1903
dotnet test                                              # expect: all green
```
