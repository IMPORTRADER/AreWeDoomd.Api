# Startup Preflight & Package Security — Design

Date: 2026-07-06
Scope: AreWeDoomd.Api solution (API + AgentService + ChatProviders)

## Goals

1. Remove the only known package vulnerability in the solution.
2. Survive Azure SQL serverless cold start (~1.5 min resume) at API startup instead of crashing on connection timeout.
3. Make ChatProvider API key configuration visible via startup logs instead of failing with exceptions.

## A. Package security

- Sole vulnerability: `Microsoft.OpenApi 2.0.0` (High, GHSA-v5pm-xwqc-g5wc — stack overflow on circular schema references). Transitive via `Microsoft.AspNetCore.OpenApi 10.0.9` (already latest).
- Fix: add a direct `<PackageReference Include="Microsoft.OpenApi" Version="2.9.0" />` to `src/AreWeDoomd.Api/AreWeDoomd.Api.csproj` to override the transitive version. Flows to test projects via project references.
- License: MIT (Microsoft), free for commercial use. Staying on the 2.x line — ASP.NET Core 10 targets the 2.x API; do not move to 3.x.
- Verify with `dotnet list package --vulnerable --include-transitive` (clean output, no NU1903 warnings).

## B. Database preflight (API project only)

Azure SQL serverless pauses when idle; the first connection triggers resume (~1.5 min) and fails with a timeout, currently taking the API down. The existing `EnableRetryOnFailure(5, 10s)` only covers per-query retries, not cold start.

### Behavior

- New file: `src/AreWeDoomd.Api/Startup/DatabasePreflight.cs` (startup concern → Api layer).
- Called from `Program.cs` immediately before `app.Run()`: `await DatabasePreflight.RunAsync(app)`. **Startup blocks here** — Kestrel does not accept requests while preflight runs.
- Each attempt: open a service scope, call `dbContext.Database.CanConnectAsync()`.
  - Success → Information log ("database ready, attempt N, elapsed X"), continue startup.
  - Failure → Warning log ("attempt N/9 failed, retrying in {delay}: {message}"), wait the scheduled delay. Each attempt also nudges the serverless DB to resume.
- All attempts exhausted → single **Error** log, stop retrying, **continue startup anyway**. The app stays up; DB-dependent requests fail and health reports unhealthy.
- Retry schedule comes from config:

```json
"Preflight": {
  "Database": {
    "RetryDelays": ["00:00:15","00:00:15","00:00:15","00:00:15","00:00:30","00:00:30","00:01:00","00:02:00"]
  }
}
```

First attempt is immediate; delays apply between attempts (9 attempts total, ~5 min). Reverting to a slower series (30s/1m/2m/5m/10m) is a config-only change.

### Health checks (new infrastructure)

- Add package `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` (Microsoft, MIT) to the Api project.
- Register `AddHealthChecks().AddDbContextCheck<AreWeDoomdDbContext>()`, map `MapHealthChecks("/health")`.
- The DbContext check queries connectivity live on each probe, so post-preflight state is always observable at `/health` even if preflight gave up.

## C. ChatProvider key startup summary (API + AgentService)

Registered providers: `gemini`, `openrouter` (Anthropic config exists in appsettings but has no implementation — ignored). Keys live at `ChatProviders:{Name}:ApiKey`, populated from env vars `GEMINI_API_KEY` / `OPENROUTER_API_KEY`.

### Shared helper

- New file: `src/AreWeDoomd.ChatProviders/ChatProviderStartupSummary.cs`, used by both hosts at startup.
- For each registered provider, check `ChatProviders:{Name}:ApiKey` non-empty and log:
  - Key present → Information: "chat provider {Name} configured".
  - Key missing → **Warning** (one line per provider).
  - No provider has a key → one **Error**: "no chat provider has an API key configured".
- Also logs the *selected* provider (API: persona generator's `Provider` option; AgentService: `Agents:ChatProvider`). If the selected provider's key is missing → **Error** (the service is non-functional without it).

### Host behavior changes

- **API**: already uses `AddChatProviders(validateOnStart: false)`; only the summary logging is added. Runtime behavior unchanged.
- **AgentService**: switch to `AddChatProviders(..., validateOnStart: false)`. Run the same summary at startup. If the selected provider has no key: log Error and **do not start the agent pipeline hosted service** — the process stays alive (no Docker restart loop, no exception), and works normally after the key is added and the service restarted. Guard key presence *before* resolving the keyed provider, since options data-annotation validation still throws on first resolve.

## Error handling summary

| Situation | Result |
|---|---|
| DB reachable within retry window | Info log, normal startup |
| DB attempt fails mid-window | Warning log, wait, retry |
| Retry window exhausted | Error log, startup continues, `/health` unhealthy |
| Some providers missing keys | Warning per provider |
| No provider keys at all | Error log |
| Selected provider missing key (API) | Error log, API runs (persona generation degraded) |
| Selected provider missing key (AgentService) | Error log, agent pipeline not started, process stays alive |

## Testing

- `DatabasePreflight`: unit tests for retry/delay sequencing and exhausted→error+continue behavior, with time/delay abstracted (no real waiting) and connection probe injectable.
- `ChatProviderStartupSummary`: unit tests for three scenarios — all keys present, partial (Warning), none (Error) — using in-memory `IConfiguration` and a capturing test logger.
- Existing tests must keep passing; verify with `dotnet test`.

## Out of scope

- Anthropic provider implementation.
- Readiness-gated request serving (user chose blocking startup instead).
- UI project.
