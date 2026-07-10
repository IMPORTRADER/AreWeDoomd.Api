# M3: Admin Auth + Read Endpoints — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A real admin identity (is_admin claim + policy + idempotent config seed), the public registerAi hole closed, and the dashboard's read surface: AI-user listing/detail, decision-log querying from the M1 JSONL files, and fleet stats (spec: `../../../docs/specs/ai-fleet-management.md` §3.2 + §6.1–6.2).

**Architecture:** Domain gains `User.GrantAdmin`. `JwtAccessTokenGenerator` emits an `is_admin=true` claim only for admins; a new `"Admin"` policy (`RequireClaim("is_admin","true")`) gates a new `AiManagementController` (`/api/admin/...`). Startup seeds admins idempotently from config (`Admin:Usernames`) and refuses the shipped default agent secret outside Development. Reading decisions: `IDecisionLogReader` (Application) + `FileDecisionLogReader` (Infrastructure) parse the AgentService's JSONL daily files (FileShare.ReadWrite, newest-first, `date:offset` cursor, torn-line skip, missing dir → `logAvailable:false`, never 500). AI-user listing via a new read repository with `EF.Functions.Like` over `TraitsJson`.

**Tech Stack:** existing stack only (MediatR, FluentValidation, Result<T>, EF Core, xUnit/Moq/Shouldly). No new packages.

## Global Constraints

- Git repo root `AreWeDoomd.Api/`; branch `feature/m3-admin-read` (off `doga/ai-dashboard`, tip 84818d3). Commands from repo root. Commits plain, NO Co-Authored-By trailer. If `git commit` is hook-blocked via bash, run the same add/commit via the PowerShell tool.
- Conventions (AGENTS.md + docs/ai): layer direction; Always-Valid domain; Result<T> known failures; FluentValidation; repositories only in handlers (no DbContext in Application); one class per file; braces; thin controllers with `this.ToActionResult`; response DTOs under `Contracts/<Feature>/`; `Offset`+`PageSize` paging with `HasMore` (no shared paged type — per-feature responses); handler clamps PageSize (`Math.Clamp(pageSize, 1, Max)`).
- Claim name literal: `"is_admin"`, value `"true"` — emitted ONLY when `User.IsAdmin` (no `is_admin=false` claims). Do NOT touch the existing `Role` claim (it carries UserType).
- Policy name literal `"Admin"` via a constants class. `AiManagementController` is policy-gated at CLASS level.
- Decision-log read contract (spec §3.2): files `decisions-{yyyy-MM-dd}.jsonl` under `DecisionLog:RootPath` (default `logs/agent-decisions`; env `DECISION_LOG_ROOT` mapped in Api Program.cs exactly like the AgentService does); open `FileShare.ReadWrite`; newest-first (newest date file first, lines within a day reversed); skip unparseable/torn lines silently; cursor string `"{yyyy-MM-dd}:{skippedFilteredItems}"` valid only for an unchanged filter set; missing/unreadable directory → empty page + `LogAvailable=false` — NEVER an exception out of the reader.
- JSONL fields are camelCase with snake_case `outcome`/`action` string values (M1 serializer) — the reader parses with `JsonSerializerDefaults.Web` into its own record; it does NOT reference AgentService types.
- Known pre-existing IntegrationTests failure (ProfileReadEndpointsTests) is unrelated — ignore; anything else failing is a problem.
- After every task: `dotnet build` + `dotnet test tests/AreWeDoomd.UnitTests` green before committing (239 tests at branch start).
- Fleet-stats caching is deliberately DEFERRED (fleet is tiny; a 30s cache is M7 polish if measurements demand it) — do not add IMemoryCache.

---

### Task 1: Domain GrantAdmin + is_admin claim in the token generator

**Files:**
- Modify: `src/AreWeDoomd.Domain/Users/User.cs`
- Modify: `src/AreWeDoomd.Infrastructure/Common/Services/JwtAccessTokenGenerator.cs`
- Test: `tests/AreWeDoomd.UnitTests/Domain/Users/UserAdminTests.cs` (create)
- Test: `tests/AreWeDoomd.UnitTests/Infrastructure/JwtAccessTokenGeneratorTests.cs` (create if none exists; if a generator test file already exists, extend it)

**Interfaces:**
- Consumes: existing `User` (has `bool IsAdmin { get; private set; }` from M2), `JwtAccessTokenGenerator` claim array.
- Produces:

```csharp
// User:
public void GrantAdmin(DateTimeOffset now); // idempotent: sets IsAdmin = true, Touch(now) only when it changes
// JwtAccessTokenGenerator: when user.IsAdmin, claims additionally contain new Claim("is_admin", "true")
// Shared literal: src/AreWeDoomd.Domain — NO; the literal lives in Api (Task 2 constants) and Infrastructure uses the raw string "is_admin" with a comment pointing at the policy. (Infrastructure must not reference Api.)
```

- [ ] **Step 1: Failing tests.** `UserAdminTests`: `GrantAdmin_ShouldSetIsAdmin` (create human or AI user → GrantAdmin(now) → IsAdmin true); `GrantAdmin_WhenAlreadyAdmin_ShouldBeIdempotent` (call twice; still true; no throw). `JwtAccessTokenGeneratorTests`: construct the generator with `Options.Create(new JwtOptions { Key = <32+ char test key>, Issuer = "t", Audience = "t" })` (read the generator to see exact dependencies); generate for an admin user → parse the JWT payload (decode with `System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token)`) → claims contain `is_admin=true`; generate for non-admin → NO `is_admin` claim. Run filters; record RED.
- [ ] **Step 2: Implement.** `GrantAdmin` per contract (guard: if already admin, return without Touch). Generator: build the claim list as a `List<Claim>`, `if (user.IsAdmin) { claims.Add(new Claim("is_admin", "true")); }`.
- [ ] **Step 3: GREEN** filtered + full suite + build.
- [ ] **Step 4: Commit** — `feat: add admin grant and is_admin JWT claim`

---

### Task 2: Admin policy + registerAi lockdown + me-endpoint IsAdmin + default-secret guard

**Files:**
- Create: `src/AreWeDoomd.Api/Auth/AuthorizationPolicies.cs` — `public static class AuthorizationPolicies { public const string Admin = "Admin"; public const string IsAdminClaim = "is_admin"; }`
- Modify: wherever `services.AddAuthorization();` lives (Api `DependencyInjection.cs` per discovery) — add the policy.
- Modify: `src/AreWeDoomd.Api/Controllers/AuthController.cs` — registerAi: `[AllowAnonymous]` → `[Authorize(Policy = AuthorizationPolicies.Admin)]`; `me` endpoint response gains IsAdmin.
- Modify: `src/AreWeDoomd.Api/Contracts/Auth/CurrentUserResponse.cs` — add `bool IsAdmin` as the last positional parameter.
- Modify: `src/AreWeDoomd.Api/Program.cs` — extend the existing SharedSecret startup guard.

**Interfaces:**
- Produces: policy registration —

```csharp
services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.Admin,
        policy => policy.RequireClaim(AuthorizationPolicies.IsAdminClaim, "true"));
});
```

- `me` endpoint: `IsAdmin = string.Equals(User.FindFirstValue(AuthorizationPolicies.IsAdminClaim), "true", StringComparison.OrdinalIgnoreCase)` (read the endpoint body first; populate the new DTO field from the claim, NOT a DB hit — consistent with how the endpoint reads other claims).
- Program.cs guard, extending the existing block that already throws on missing secret:

```csharp
const string defaultDevSecret = "dev-agent-shared-secret-change-me";
if (!builder.Environment.IsDevelopment() &&
    string.Equals(agentSecret, defaultDevSecret, StringComparison.Ordinal))
{
    throw new InvalidOperationException(
        "AgentNotifications:SharedSecret still has the shipped development default; refusing to start outside Development.");
}
```

- registerHuman stays public. AgentSecret-authenticated principals have no `is_admin` claim → agents cannot call admin endpoints; that is correct and intentional.

**Steps:**
- [ ] **Step 1:** No handler logic → no new unit tests beyond compile-driven updates; BUT if any existing test constructs `CurrentUserResponse`, fix positionally. Read AuthController fully before editing.
- [ ] **Step 2:** Implement all five file changes.
- [ ] **Step 3:** `dotnet build` 0 errors; full unit suite green.
- [ ] **Step 4: Commit** — `feat: add Admin policy, lock registerAi, expose IsAdmin on me endpoint`

---

### Task 3: Idempotent admin seeding from config

**Files:**
- Create: `src/AreWeDoomd.Api/Auth/AdminOptions.cs` — `public sealed class AdminOptions { public const string SectionName = "Admin"; public IReadOnlyList<string> Usernames { get; set; } = []; }`
- Create: `src/AreWeDoomd.Api/Auth/AdminSeeder.cs` — static class with `public static async Task SeedAsync(WebApplication app)`.
- Modify: `src/AreWeDoomd.Api/Program.cs` — bind options; call `await AdminSeeder.SeedAsync(app);` after `Build()` before `Run()` (mirror where other startup work happens).
- Modify: `src/AreWeDoomd.Api/appsettings.json` — add `"Admin": { "Usernames": [] }` (documented empty default; the operator sets their username here or via `Admin__Usernames__0` env var).
- Test: `tests/AreWeDoomd.UnitTests/Api/Auth/AdminSeederTests.cs` — only if the seeder core is factored testable; REQUIRED factoring: put the per-user logic in `internal static async Task<int> SeedCoreAsync(IReadOnlyList<string> usernames, IUserRepository users, IUnitOfWork unitOfWork, DateTimeOffset now, ILogger logger, CancellationToken ct)` and unit-test THAT with mocks (public wrapper resolves scope + options and delegates).

**Behavior contract:** for each configured username: `GetByUsernameAsync` → missing → LogWarning("admin seed: user {Username} not found — skipped") ; found && already admin → skip silently; found && not admin → `GrantAdmin(now)` + `UpdateAsync` ; after the loop, ONE `SaveChangesAsync` if anything changed; returns count granted. Empty config → no-op, no scope errors. Never throws for missing users (config typos must not kill startup); repository/database exceptions propagate (fail fast — DB down at boot is a real failure).

- [ ] **Step 1: Failing tests** (`SeedCoreAsync` with mocked IUserRepository/IUnitOfWork): grants when found-not-admin (verify UpdateAsync + SaveChanges once, returns 1); skips missing user without throwing (SaveChanges never, returns 0); idempotent when already admin (no Update, no Save). Record RED.
- [ ] **Step 2: Implement** seeder + wiring + appsettings.
- [ ] **Step 3: GREEN** + full suite + build.
- [ ] **Step 4: Commit** — `feat: seed admin users idempotently from config at startup`

---

### Task 4: IDecisionLogReader (Application) + FileDecisionLogReader (Infrastructure)

**Files:**
- Create: `src/AreWeDoomd.Application/Common/Interfaces/IDecisionLogReader.cs`
- Create: `src/AreWeDoomd.Application/Common/Models/DecisionLogRecord.cs`
- Create: `src/AreWeDoomd.Application/Common/Models/DecisionLogFilter.cs`
- Create: `src/AreWeDoomd.Application/Common/Models/DecisionLogPage.cs`
- Create: `src/AreWeDoomd.Application/Common/Models/DecisionLogDailyStats.cs`
- Create: `src/AreWeDoomd.Infrastructure/Common/Services/FileDecisionLogReader.cs`
- Create: `src/AreWeDoomd.Infrastructure/Common/Options/DecisionLogOptions.cs` (Api-side options: SectionName "DecisionLog", `RootPath` default `Path.Combine("logs","agent-decisions")`)
- Modify: Infrastructure `DependencyInjection.cs` — bind options + `services.AddSingleton<IDecisionLogReader, FileDecisionLogReader>();`
- Modify: `src/AreWeDoomd.Api/Program.cs` — DECISION_LOG_ROOT env mapping onto `DecisionLog:RootPath` (same in-memory-collection pattern the AgentService Program.cs uses — copy it).
- Test: `tests/AreWeDoomd.UnitTests/Infrastructure/FileDecisionLogReaderTests.cs`

**Interfaces (exact — Tasks 6–7 depend on these):**

```csharp
public sealed record DecisionLogRecord(
    DateTimeOffset Ts, string AiUserId, string ActivityId, string ActivityType, string Outcome,
    string? Action, string? Reasoning, string? Content, Guid? PostId, Guid? CommentId,
    string? Priority, string? ErrorDetail, int? LlmAttempts, int? PersonaVersion,
    string? PersonaSource, string? SessionLogRef);

public sealed record DecisionLogFilter(
    string? AiUserId = null, string? Action = null, string? Outcome = null,
    DateOnly? FromUtc = null, DateOnly? ToUtc = null);

public sealed record DecisionLogPage(
    IReadOnlyList<DecisionLogRecord> Items, string? NextCursor, bool HasMore, bool LogAvailable);

public sealed record DecisionLogDailyStats(
    DateOnly DateUtc, int Total, int Executed, int Dropped, int Failed, int ActionsLastHour);
    // Failed = llm_failed + action_failed; ActionsLastHour = entries with Ts within the trailing
    // 60 minutes of 'now' passed by the caller — signature below takes nowUtc for determinism.

public interface IDecisionLogReader
{
    Task<DecisionLogPage> ReadAsync(DecisionLogFilter filter, string? cursor, int pageSize, CancellationToken ct);
    Task<DecisionLogDailyStats?> GetDailyStatsAsync(DateOnly dateUtc, DateTimeOffset nowUtc, CancellationToken ct);
    // GetDailyStatsAsync returns null when the day file is missing OR the log dir is unavailable.
}
```

**Reader algorithm (FileDecisionLogReader):**
- Root missing → `new DecisionLogPage([], null, false, LogAvailable: false)`.
- Determine candidate dates: enumerate `decisions-*.jsonl`, parse dates from filenames (invariant, exact `yyyy-MM-dd`), intersect with `[FromUtc..ToUtc]` when set, sort DESCENDING. Cursor `"{date}:{skip}"`: start at that date with that many filtered items skipped (dates newer than the cursor date are excluded — they were already served).
- Per date: read all lines (`FileStream` with `FileShare.ReadWrite` + StreamReader), parse each via `JsonSerializer.Deserialize<DecisionLogRecord>(line, Web)` inside try/catch — parse failure or blank → skip; apply filter (AiUserId exact, Action exact, Outcome exact — all case-insensitive ordinal-ignore-case); REVERSE (files are chronological-append; page is newest-first); apply skip; take remaining needed.
- NextCursor: if more filtered items remain in the current date → `"{date}:{alreadyServedInDate}"`; else if older candidate dates remain → `"{olderDate}:0"`; else null. `HasMore = NextCursor != null`.
- All IO wrapped so ANY unexpected exception (locked file etc.) degrades: log warning, return what was gathered with `LogAvailable = true` if the dir existed (partial read is fine) — the reader never throws.
- `GetDailyStatsAsync`: read the one day file (same parse rules); count Total / outcome=="executed" / "dropped" / ("llm_failed" or "action_failed"); ActionsLastHour = count of entries with `Outcome=="executed"` and `Ts >= nowUtc.AddHours(-1)`. Missing file/dir → null.

- [ ] **Step 1: Failing tests** (temp-dir pattern from `DecisionLogFileAppenderTests` — write JSONL lines with raw strings matching the M1 format):
  1. `ReadAsync_WhenDirectoryMissing_ShouldReturnUnavailablePage` (LogAvailable false, empty, null cursor, no throw).
  2. `ReadAsync_ShouldReturnNewestFirstAcrossDays` (2 files, 2 lines each; first page pageSize 3 → newest day's lines reversed first, then next day's newest; NextCursor points into old day).
  3. `ReadAsync_WithCursor_ShouldContinueWithoutOverlap` (page 1 pageSize 2 then page 2 with returned cursor → no duplicates, correct order, final NextCursor null).
  4. `ReadAsync_ShouldApplyFilters` (mixed aiUserId/outcome lines; filter by AiUserId + Outcome executed → only matches).
  5. `ReadAsync_ShouldSkipTornAndInvalidLines` (file containing a valid line, a torn half-JSON line, an empty line → only the valid record returned).
  6. `GetDailyStatsAsync_ShouldCountOutcomes` (day file with executed×2 [one within last hour of the passed now], dropped×1, llm_failed×1 → Total 4, Executed 2, Dropped 1, Failed 1, ActionsLastHour 1).
  7. `GetDailyStatsAsync_WhenFileMissing_ShouldReturnNull`.
  Record RED.
- [ ] **Step 2: Implement** reader + options + DI + env mapping.
- [ ] **Step 3: GREEN** (7/7) + full suite + build.
- [ ] **Step 4: Commit** — `feat: add decision log reader with cursor paging over JSONL files`

---

### Task 5: AI-user read repository + ListAiUsers/GetAiUserDetail queries

**Files:**
- Create: `src/AreWeDoomd.Application/Common/Interfaces/IAiUserReadRepository.cs`
- Create: `src/AreWeDoomd.Application/Common/Models/AiUserListItem.cs`
- Create: `src/AreWeDoomd.Infrastructure/Common/Repositories/AiUserReadRepository.cs` (+ DI registration alongside the other repositories)
- Create: `src/AreWeDoomd.Application/Features/AiManagement/Queries/ListAiUsers/` — `ListAiUsersQuery.cs`, `ListAiUsersQueryHandler.cs`, `ListAiUsersQueryValidator.cs`, `AiUserListResult.cs`
- Create: `src/AreWeDoomd.Application/Features/AiManagement/Queries/GetAiUserDetail/` — `GetAiUserDetailQuery.cs`, `GetAiUserDetailQueryHandler.cs`, `GetAiUserDetailQueryValidator.cs`, `AiUserDetailResult.cs`
- Tests: `tests/AreWeDoomd.UnitTests/Application/AiManagement/ListAiUsersQueryHandlerTests.cs`, `GetAiUserDetailQueryHandlerTests.cs`

**Interfaces (exact):**

```csharp
public sealed record AiUserListItem(
    Guid Id, string Username, string? ProfileImageUrl, DateTimeOffset CreatedAt,
    bool HasPersonality, IReadOnlyList<string> Traits, string? TypingStyle, int? PersonaVersion);

public interface IAiUserReadRepository
{
    Task<(IReadOnlyList<AiUserListItem> Items, int TotalCount)> ListAsync(
        string? trait, string? search, int offset, int pageSize, CancellationToken ct);
    // trait: EF.Functions.Like(TraitsJson, $"%\"%{trait}%\"%") — matches inside a JSON string element, case per SQL collation
    // search: Like on Username. Both null → all AI users. Filter: UserType == Ai only. Order: CreatedAt DESC.
}

public sealed record ListAiUsersQuery(string? Trait, string? Search, int Offset, int PageSize)
    : IRequest<Result<AiUserListResult>>;
public sealed record AiUserListResult(IReadOnlyList<AiUserListItem> Items, int TotalCount, bool HasMore);

public sealed record GetAiUserDetailQuery(Guid UserId) : IRequest<Result<AiUserDetailResult>>;
public sealed record AiUserDetailResult(
    Guid Id, string Username, string Email, string? ProfileImageUrl, string? Biography,
    DateTimeOffset CreatedAt, bool HasPersonality, IReadOnlyList<string> Traits,
    string? TypingStyle, string? Summary, int? PersonaVersion, DateTimeOffset? PersonaUpdatedAt);
```

Handler notes: List handler clamps `PageSize` to `Math.Clamp(pageSize, 1, 100)`, floors Offset at 0, `HasMore = offset + items.Count < totalCount`. Detail handler: `IUserRepository.GetByIdAsync` → null or non-Ai → NotFound (identical payload, code "ai_user.not_found"); maps persona fields (nulls when no personality). Validators: Offset ≥ 0, PageSize ≥ 1; UserId NotEmpty. Repository impl mirrors `ProfileStatsRepository` (ctor takes `AreWeDoomdDbContext`), projects the owned entity directly in the query (select new AiUserListItem — note Traits must be materialized from `AiPersonality.Traits` AFTER fetching entities, since the computed property can't translate: fetch entities/anonymous projection with TraitsJson then map in memory).

- [ ] **Step 1: Failing handler tests** (mock IAiUserReadRepository / IUserRepository): list happy path maps + HasMore math (TotalCount 5, offset 0, pageSize 2 → HasMore true); clamp test (PageSize 500 → repo called with 100); detail NotFound for human; detail success maps persona fields; detail without personality → HasPersonality false, null persona fields. Record RED.
- [ ] **Step 2: Implement** all files + DI.
- [ ] **Step 3: GREEN** + full suite + build.
- [ ] **Step 4: Commit** — `feat: add AI user list and detail queries for the admin dashboard`

---

### Task 6: GetAgentDecisions + GetAiFleetStats queries

**Files:**
- Create: `src/AreWeDoomd.Application/Features/AiManagement/Queries/GetAgentDecisions/` — Query (`Guid? AiUserId, string? Action, string? Outcome, DateOnly? FromUtc, DateOnly? ToUtc, string? Cursor, int PageSize`), Handler, Validator, `AgentDecisionsResult.cs` (`IReadOnlyList<DecisionLogRecord> Items, string? NextCursor, bool HasMore, bool LogAvailable`)
- Create: `src/AreWeDoomd.Application/Features/AiManagement/Queries/GetAiFleetStats/` — Query (parameterless record), Handler, `AiFleetStatsResult.cs` (`int TotalAiUsers, int WithPersonality, int DecisionsToday, int ExecutedToday, int DroppedToday, int FailedToday, int ActionsLastHour, bool LogAvailable`)
- Modify: `src/AreWeDoomd.Application/Common/Interfaces/IAiUserReadRepository.cs` — add `Task<(int Total, int WithPersonality)> CountAsync(CancellationToken ct);` + impl.
- Tests: `GetAgentDecisionsQueryHandlerTests.cs`, `GetAiFleetStatsQueryHandlerTests.cs`

**Contracts:** Decisions handler: clamp PageSize to `Math.Clamp(..., 1, 100)`; build `DecisionLogFilter(AiUserId?.ToString(), Action, Outcome, FromUtc, ToUtc)`; pass cursor through; wrap reader page in Result.Success ALWAYS (LogAvailable=false is a successful, empty answer — never an error Result). Stats handler: `CountAsync` + `GetDailyStatsAsync(DateOnly.FromDateTime(nowUtc.UtcDateTime), nowUtc, ct)` where nowUtc comes from the codebase's `IDateTimeProvider` if one exists (discovery says GetGlobalFeed uses IDateTimeProvider — reuse it), null stats → zeros + LogAvailable=false. Validator: PageSize ≥ 1; FromUtc ≤ ToUtc when both set.

- [ ] **Step 1: Failing tests** (mock IDecisionLogReader/IAiUserReadRepository/IDateTimeProvider): decisions pass-through incl. cursor + LogAvailable false path stays Success; filter mapping (Guid→string); stats aggregation happy path; stats null-day → zeros + LogAvailable false. RED.
- [ ] **Step 2: Implement.**
- [ ] **Step 3: GREEN** + suite + build.
- [ ] **Step 4: Commit** — `feat: add decision feed and fleet stats queries`

---

### Task 7: AiManagementController + response DTOs + Postman + final sweep

**Files:**
- Create: `src/AreWeDoomd.Api/Controllers/AiManagementController.cs`
- Create: `src/AreWeDoomd.Api/Contracts/Admin/` — `AiUserListResponse.cs`, `AiUserItemResponse.cs`, `AiUserDetailResponse.cs`, `AgentDecisionsResponse.cs`, `AgentDecisionItemResponse.cs`, `AiFleetStatsResponse.cs` (mirror the Application result shapes 1:1; one type per file)
- Modify: `postman/AreWeDoomd.Api.postman_collection.json` — "AI Management" folder: the four GETs with an admin bearer token note.

**Controller shape:**

```csharp
[ApiController]
[Route("api/admin")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
public sealed class AiManagementController(IMediator mediator) : ControllerBase
{
    [HttpGet("ai-users")]           // ?trait=&search=&offset=0&pageSize=20
    [HttpGet("ai-users/{userId:guid}")]
    [HttpGet("decisions")]          // ?aiUserId=&action=&outcome=&fromUtc=&toUtc=&cursor=&pageSize=20
    [HttpGet("ai-stats")]
}
```

Each action: bind query params, `mediator.Send`, `this.ToActionResult(result, Map...)`. ProducesResponseType 200/401/403 (+404 on detail).

- [ ] **Step 1:** Implement controller + DTOs (read CommentsController + FeedController for exact attribute/binding style first).
- [ ] **Step 2:** `dotnet build`; full unit suite.
- [ ] **Step 3:** Postman folder added.
- [ ] **Step 4: Final sweep:** `dotnet build --no-incremental` 0 errors; `dotnet test` both projects (only the known IntegrationTests failure); `git grep -n "is_admin"` — hits only in AuthorizationPolicies, JwtAccessTokenGenerator, AuthController/me, tests (no stray literals elsewhere); `git grep -n "AllowAnonymous" src/AreWeDoomd.Api/Controllers/AuthController.cs` — registerAi no longer carries it.
- [ ] **Step 5: Commit** — `feat: add admin AI management read endpoints`

---

## Self-Review Notes

- **Spec §6.1:** is_admin dedicated claim (Role untouched) ✓; policy ✓; idempotent config seed (no out-of-band SQL) ✓; IsAdmin on /api/auth/me ✓; registerAi locked ✓; default-secret refusal outside Development ✓. Admin grant takes effect at next login (accepted, spec).
- **Spec §3.2/§6.2:** reader contract (ReadWrite share, torn-line skip, date:offset cursor, newest-first, logAvailable:false-never-500) ✓; DECISION_LOG_ROOT env in Api ✓; list/detail/decisions/stats endpoints ✓ (stats includes dropped count for the M4 FleetStatsBar) ✓; trait LIKE filter over TraitsJson ✓ (documented pragmatic choice).
- **Deliberate deviations:** (a) stats caching deferred to M7 (tiny fleet; YAGNI) — flagged in Global Constraints; (b) decisions endpoint also filters by Outcome (cheap, dashboard will want it) — small scope addition, called out to reviewer; (c) reader partial-read degradation returns gathered items rather than failing (matches never-500 contract).
- **Type consistency check:** DecisionLogRecord fields = M1 JSONL schema (camelCase Web parse; outcome/action are snake_case STRING VALUES — record property type string, not enum, so no converter needed); cursor format identical in Task 4 algorithm and Task 6 pass-through; AuthorizationPolicies literals used in Tasks 2 and 7.
- **Cross-milestone:** M4 will consume every endpoint added here; UpdateAiPersonalityCommand deliberately NOT in M3 (it's M4's backend piece per the milestone table).
