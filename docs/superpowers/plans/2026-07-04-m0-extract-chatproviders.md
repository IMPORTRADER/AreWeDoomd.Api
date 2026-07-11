# M0: Extract AreWeDoomd.ChatProviders Shared Library — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the provider-agnostic LLM chat layer out of `AreWeDoomd.AgentService` into a new class library `AreWeDoomd.ChatProviders` so the Api (Infrastructure) can later reuse it for bulk persona generation (spec: `../../../docs/specs/ai-fleet-management.md` §5 — note: the spec lives in the repo root above this git repo).

**Architecture:** Pure move/refactor, zero behavior change. The `Ai/` folder of AgentService currently holds two groups: (a) the provider-agnostic chat abstraction (`IChatProvider`, request/result records, options base, Gemini + OpenRouter adapters, DI extensions) and (b) AgentService-specific session logging (`IAiSessionLogger`/`AiSessionLogger`). Group (a) moves to the new project under namespace `AreWeDoomd.ChatProviders`; group (b) stays. Config section names (`ChatProviders:Gemini`, `ChatProviders:OpenRouter`), provider keys (`"gemini"`, `"openrouter"`), and all runtime behavior stay identical.

**Tech Stack:** .NET 10 class library, Microsoft.Extensions.Http / Options.DataAnnotations / Options.ConfigurationExtensions (all 10.0.9, matching existing versions).

## Global Constraints

- Git repo root is `AreWeDoomd.Api/` (the outer `AreWeDoomd/` folder is NOT a git repo). All `git` and `dotnet` commands run from `AreWeDoomd.Api/`.
- Backend rules (AGENTS.md): one class per file; braces always required; namespaces mirror folders.
- Commit messages: plain, no `Co-Authored-By` trailer (user's global rule).
- No behavior change anywhere in this plan. No config file changes. The safety net is the existing unit test suite (`GeminiProviderTests`, `OpenRouterProviderTests`, `AgentEventProcessorTests`).
- Config section names `ChatProviders:Gemini` / `ChatProviders:OpenRouter` and provider name constants `"gemini"` / `"openrouter"` must not change.
- Use `git mv` for every file move so history is preserved.
- After every task: `dotnet build` and `dotnet test` must both succeed before committing.
- Shell is PowerShell (no `&&` — use `;`).

## File Inventory (source of truth for the whole plan)

All paths relative to `AreWeDoomd.Api/`. Current namespace → new namespace:

| Group | Files (in `src/AreWeDoomd.AgentService/Ai/`) | Old namespace | New namespace |
|---|---|---|---|
| Core (Task 2) | `IChatProvider.cs`, `ChatRequest.cs`, `ChatResult.cs`, `ChatMessage.cs`, `ChatError.cs`, `FinishReason.cs`, `TokenUsage.cs`, `ChatProviderOptions.cs` | `AreWeDoomd.AgentService.Ai` | `AreWeDoomd.ChatProviders` |
| Gemini (Task 3) | `Providers/Gemini/GeminiProvider.cs`, `Providers/Gemini/GeminiProviderOptions.cs` | `AreWeDoomd.AgentService.Ai.Providers.Gemini` | `AreWeDoomd.ChatProviders.Providers.Gemini` |
| Gemini wire (Task 3) | `Providers/Gemini/Wire/` — `GeminiCandidate.cs`, `GeminiContent.cs`, `GeminiError.cs`, `GeminiErrorResponse.cs`, `GeminiGenerateContentRequest.cs`, `GeminiGenerateContentResponse.cs`, `GeminiGenerationConfig.cs`, `GeminiPart.cs`, `GeminiUsageMetadata.cs` | `AreWeDoomd.AgentService.Ai.Providers.Gemini.Wire` | `AreWeDoomd.ChatProviders.Providers.Gemini.Wire` |
| OpenRouter (Task 4) | `Providers/OpenRouter/OpenRouterProvider.cs`, `Providers/OpenRouter/OpenRouterProviderOptions.cs` | `AreWeDoomd.AgentService.Ai.Providers.OpenRouter` | `AreWeDoomd.ChatProviders.Providers.OpenRouter` |
| OpenRouter wire (Task 4) | `Providers/OpenRouter/Wire/` — `OpenRouterChatCompletionRequest.cs`, `OpenRouterChatCompletionResponse.cs`, `OpenRouterChoice.cs`, `OpenRouterError.cs`, `OpenRouterErrorResponse.cs`, `OpenRouterJsonSchema.cs`, `OpenRouterMessage.cs`, `OpenRouterResponseFormat.cs`, `OpenRouterResponseMessage.cs`, `OpenRouterUsage.cs` | `AreWeDoomd.AgentService.Ai.Providers.OpenRouter.Wire` | `AreWeDoomd.ChatProviders.Providers.OpenRouter.Wire` |
| DI extensions (Task 5) | `ChatProviderServiceCollectionExtensions.cs` | `AreWeDoomd.AgentService.Ai` | `AreWeDoomd.ChatProviders` |
| **STAYS** in AgentService | `AiSessionLogger.cs`, `IAiSessionLogger.cs` | `AreWeDoomd.AgentService.Ai` | unchanged (gains `using AreWeDoomd.ChatProviders;`) |

Consumers that reference moved types (each gains `using AreWeDoomd.ChatProviders;` in Task 2; existing `using AreWeDoomd.AgentService.Ai;` lines are KEPT wherever `IAiSessionLogger` is still used):

- `src/AreWeDoomd.AgentService/Program.cs` (uses `IChatProvider` at line ~73 and `IAiSessionLogger` — keeps both usings)
- `src/AreWeDoomd.AgentService/Processing/AgentEventProcessor.cs` (uses `IChatProvider`, `ChatRequest`, `ChatResult`, `IAiSessionLogger` — keeps both usings)
- `src/AreWeDoomd.AgentService/Ai/AiSessionLogger.cs` + `IAiSessionLogger.cs` (signatures use `ChatRequest`/`ChatResult`)
- `tests/AreWeDoomd.UnitTests/AgentService/Processing/AgentEventProcessorTests.cs`
- `tests/AreWeDoomd.UnitTests/AgentService/Ai/GeminiProviderTests.cs` (moves in Task 3)
- `tests/AreWeDoomd.UnitTests/AgentService/Ai/OpenRouterProviderTests.cs` (moves in Task 4)

---

### Task 1: Scaffold the AreWeDoomd.ChatProviders project

**Files:**
- Create: `src/AreWeDoomd.ChatProviders/AreWeDoomd.ChatProviders.csproj`
- Modify: `AreWeDoomd.Api.slnx`
- Modify: `src/AreWeDoomd.AgentService/AreWeDoomd.AgentService.csproj`
- Modify: `tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj`
- Modify: `src/AreWeDoomd.AgentService/Dockerfile`

**Interfaces:**
- Consumes: nothing.
- Produces: an empty, referenced class library that Tasks 2–5 move files into. Project name/assembly/root namespace: `AreWeDoomd.ChatProviders`.

- [ ] **Step 1: Create the csproj**

`src/AreWeDoomd.ChatProviders/AreWeDoomd.ChatProviders.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.9" />
    <PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="10.0.9" />
    <PackageReference Include="Microsoft.Extensions.Options.DataAnnotations" Version="10.0.9" />
  </ItemGroup>

</Project>
```

(These three packages cover everything the moved code needs: `IHttpClientFactory`/`AddHttpClient`, `IConfiguration.GetSection`/`Bind`, `ValidateDataAnnotations`, `ValidateOnStart`, `AddKeyedSingleton`, `ILogger<T>` — the rest arrives transitively. Contingency: if the Task 5 build cannot find `ValidateOnStart`, add `<PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="10.0.9" />`.)

- [ ] **Step 2: Add the project to the solution**

In `AreWeDoomd.Api.slnx`, inside the `/src/` folder element, add one line after the Application project entry:

```xml
    <Project Path="src/AreWeDoomd.ChatProviders/AreWeDoomd.ChatProviders.csproj" />
```

- [ ] **Step 3: Reference it from AgentService and UnitTests**

In `src/AreWeDoomd.AgentService/AreWeDoomd.AgentService.csproj`, add to the existing `<ItemGroup>` with the Contracts ProjectReference:

```xml
    <ProjectReference Include="..\AreWeDoomd.ChatProviders\AreWeDoomd.ChatProviders.csproj" />
```

In `tests/AreWeDoomd.UnitTests/AreWeDoomd.UnitTests.csproj`, add to the ProjectReference ItemGroup:

```xml
    <ProjectReference Include="..\..\src\AreWeDoomd.ChatProviders\AreWeDoomd.ChatProviders.csproj" />
```

(Do NOT reference it from Infrastructure yet — that happens in M6 when the first consumer exists. YAGNI.)

- [ ] **Step 4: Fix the AgentService Dockerfile restore layer**

In `src/AreWeDoomd.AgentService/Dockerfile`, after the Contracts csproj COPY (line 8), add:

```dockerfile
COPY src/AreWeDoomd.ChatProviders/AreWeDoomd.ChatProviders.csproj src/AreWeDoomd.ChatProviders/
```

- [ ] **Step 5: Build and test**

Run (from `AreWeDoomd.Api/`): `dotnet build; dotnet test tests/AreWeDoomd.UnitTests`
Expected: build succeeds (new project compiles empty), all existing tests pass.

- [ ] **Step 6: Commit**

```powershell
git add -A; git commit -m "chore: scaffold AreWeDoomd.ChatProviders class library"
```

---

### Task 2: Move the core chat abstraction (8 files)

**Files:**
- Move (git mv): the 8 "Core" files from the inventory table → `src/AreWeDoomd.ChatProviders/` (project root, no subfolder — they are the library's public surface)
- Modify: namespace line in each moved file
- Modify (add `using AreWeDoomd.ChatProviders;`): `src/AreWeDoomd.AgentService/Ai/IAiSessionLogger.cs`, `src/AreWeDoomd.AgentService/Ai/AiSessionLogger.cs`, `src/AreWeDoomd.AgentService/Ai/ChatProviderServiceCollectionExtensions.cs`, `src/AreWeDoomd.AgentService/Ai/Providers/Gemini/GeminiProvider.cs`, `src/AreWeDoomd.AgentService/Ai/Providers/Gemini/GeminiProviderOptions.cs`, `src/AreWeDoomd.AgentService/Ai/Providers/OpenRouter/OpenRouterProvider.cs`, `src/AreWeDoomd.AgentService/Ai/Providers/OpenRouter/OpenRouterProviderOptions.cs`, `src/AreWeDoomd.AgentService/Program.cs`, `src/AreWeDoomd.AgentService/Processing/AgentEventProcessor.cs`, `tests/AreWeDoomd.UnitTests/AgentService/Processing/AgentEventProcessorTests.cs`, `tests/AreWeDoomd.UnitTests/AgentService/Ai/GeminiProviderTests.cs`, `tests/AreWeDoomd.UnitTests/AgentService/Ai/OpenRouterProviderTests.cs`

**Interfaces:**
- Consumes: the empty project from Task 1.
- Produces: `AreWeDoomd.ChatProviders.IChatProvider` (`string Name { get; }`, `Task<ChatResult> CompleteAsync(ChatRequest request, CancellationToken ct)`), records `ChatRequest(string Model, IReadOnlyList<ChatMessage> Messages, string? System, int? MaxTokens, double? Temperature, string? JsonResponseSchema)`, `ChatResult` (factories `Ok(text, usage, finish)` / `Fail(ChatError)`), `ChatMessage`, `ChatError(string Message, int? StatusCode, string Provider)`, `FinishReason` enum, `TokenUsage(int InputTokens, int OutputTokens)`, abstract `ChatProviderOptions` (ApiKey/BaseUrl/DefaultModel/DefaultMaxTokens). Exact bodies are unchanged from the originals — only the namespace line changes.

- [ ] **Step 1: Move the files**

```powershell
git mv src/AreWeDoomd.AgentService/Ai/IChatProvider.cs src/AreWeDoomd.ChatProviders/
git mv src/AreWeDoomd.AgentService/Ai/ChatRequest.cs src/AreWeDoomd.ChatProviders/
git mv src/AreWeDoomd.AgentService/Ai/ChatResult.cs src/AreWeDoomd.ChatProviders/
git mv src/AreWeDoomd.AgentService/Ai/ChatMessage.cs src/AreWeDoomd.ChatProviders/
git mv src/AreWeDoomd.AgentService/Ai/ChatError.cs src/AreWeDoomd.ChatProviders/
git mv src/AreWeDoomd.AgentService/Ai/FinishReason.cs src/AreWeDoomd.ChatProviders/
git mv src/AreWeDoomd.AgentService/Ai/TokenUsage.cs src/AreWeDoomd.ChatProviders/
git mv src/AreWeDoomd.AgentService/Ai/ChatProviderOptions.cs src/AreWeDoomd.ChatProviders/
```

- [ ] **Step 2: Change the namespace in each moved file**

In each of the 8 files, replace the line `namespace AreWeDoomd.AgentService.Ai;` with:

```csharp
namespace AreWeDoomd.ChatProviders;
```

No other line changes.

- [ ] **Step 3: Add the using to every consumer left behind**

In each of the 12 "Modify (add using)" files listed above, add `using AreWeDoomd.ChatProviders;` to the using block (alphabetical position; for files that currently have no usings, e.g. `IAiSessionLogger.cs` and the provider options files, add it above the namespace line). KEEP existing `using AreWeDoomd.AgentService.Ai;` lines — `IAiSessionLogger` and (until Task 5) `AddChatProviders` still live there. Example, `IAiSessionLogger.cs` becomes:

```csharp
using AreWeDoomd.ChatProviders;

namespace AreWeDoomd.AgentService.Ai;

public interface IAiSessionLogger
{
    void Log(string activityId, int attempt, ChatRequest request, ChatResult result);
}
```

- [ ] **Step 4: Build and test**

Run: `dotnet build; dotnet test tests/AreWeDoomd.UnitTests`
Expected: build succeeds, all tests pass (same counts as Task 1).

- [ ] **Step 5: Commit**

```powershell
git add -A; git commit -m "refactor: move core chat abstraction to AreWeDoomd.ChatProviders"
```

---

### Task 3: Move the Gemini adapter

**Files:**
- Move: `src/AreWeDoomd.AgentService/Ai/Providers/Gemini/**` (11 files: provider, options, 9 wire DTOs) → `src/AreWeDoomd.ChatProviders/Providers/Gemini/**` (same relative layout)
- Move: `tests/AreWeDoomd.UnitTests/AgentService/Ai/GeminiProviderTests.cs` → `tests/AreWeDoomd.UnitTests/ChatProviders/GeminiProviderTests.cs`
- Modify: `src/AreWeDoomd.AgentService/Ai/ChatProviderServiceCollectionExtensions.cs` (using swap)

**Interfaces:**
- Consumes: core types from Task 2 (`IChatProvider`, `ChatResult`, `ChatProviderOptions`, …).
- Produces: `AreWeDoomd.ChatProviders.Providers.Gemini.GeminiProvider` (constants `ProviderName = "gemini"`, `HttpClientName = "gemini"`) and `GeminiProviderOptions` (`SectionName = "ChatProviders:Gemini"`) — values unchanged.

- [ ] **Step 1: Move source files**

```powershell
New-Item -ItemType Directory -Force src/AreWeDoomd.ChatProviders/Providers | Out-Null
git mv src/AreWeDoomd.AgentService/Ai/Providers/Gemini src/AreWeDoomd.ChatProviders/Providers/Gemini
```

- [ ] **Step 2: Update namespaces in the 11 moved files**

- `GeminiProvider.cs`, `GeminiProviderOptions.cs`: `namespace AreWeDoomd.AgentService.Ai.Providers.Gemini;` → `namespace AreWeDoomd.ChatProviders.Providers.Gemini;`
- The 9 files under `Wire/`: `namespace AreWeDoomd.AgentService.Ai.Providers.Gemini.Wire;` → `namespace AreWeDoomd.ChatProviders.Providers.Gemini.Wire;`
- In `GeminiProvider.cs` also fix its wire using: `using AreWeDoomd.AgentService.Ai.Providers.Gemini.Wire;` → `using AreWeDoomd.ChatProviders.Providers.Gemini.Wire;` — and DELETE the `using AreWeDoomd.ChatProviders;` line added in Task 2 (core types are now namespace-visible from the parent namespace).
- In `GeminiProviderOptions.cs`: delete the Task 2 `using AreWeDoomd.ChatProviders;` line (same reason).

- [ ] **Step 3: Fix the DI extensions using**

In `src/AreWeDoomd.AgentService/Ai/ChatProviderServiceCollectionExtensions.cs`, replace `using AreWeDoomd.AgentService.Ai.Providers.Gemini;` with `using AreWeDoomd.ChatProviders.Providers.Gemini;`.

- [ ] **Step 4: Move and fix the test file**

```powershell
New-Item -ItemType Directory -Force tests/AreWeDoomd.UnitTests/ChatProviders | Out-Null
git mv tests/AreWeDoomd.UnitTests/AgentService/Ai/GeminiProviderTests.cs tests/AreWeDoomd.UnitTests/ChatProviders/
```

In the moved file: replace `using AreWeDoomd.AgentService.Ai;` with `using AreWeDoomd.ChatProviders;` (and drop the duplicate if Task 2 already added it), replace `using AreWeDoomd.AgentService.Ai.Providers.Gemini;` with `using AreWeDoomd.ChatProviders.Providers.Gemini;`, and change the file's namespace declaration to end in `.ChatProviders` (mirror the test project's existing folder→namespace pattern, e.g. `AreWeDoomd.UnitTests.ChatProviders`).

- [ ] **Step 5: Build and test**

Run: `dotnet build; dotnet test tests/AreWeDoomd.UnitTests --filter "FullyQualifiedName~GeminiProvider"`
Expected: build succeeds; Gemini provider tests pass. Then run the full suite: `dotnet test tests/AreWeDoomd.UnitTests` — all pass.

- [ ] **Step 6: Commit**

```powershell
git add -A; git commit -m "refactor: move Gemini adapter to AreWeDoomd.ChatProviders"
```

---

### Task 4: Move the OpenRouter adapter

Identical shape to Task 3.

**Files:**
- Move: `src/AreWeDoomd.AgentService/Ai/Providers/OpenRouter/**` (12 files: provider, options, 10 wire DTOs) → `src/AreWeDoomd.ChatProviders/Providers/OpenRouter/**`
- Move: `tests/AreWeDoomd.UnitTests/AgentService/Ai/OpenRouterProviderTests.cs` → `tests/AreWeDoomd.UnitTests/ChatProviders/OpenRouterProviderTests.cs`
- Modify: `src/AreWeDoomd.AgentService/Ai/ChatProviderServiceCollectionExtensions.cs` (using swap)

**Interfaces:**
- Consumes: core types from Task 2.
- Produces: `AreWeDoomd.ChatProviders.Providers.OpenRouter.OpenRouterProvider` (constants `ProviderName = "openrouter"`, `HttpClientName = "openrouter"`) and `OpenRouterProviderOptions` (`SectionName = "ChatProviders:OpenRouter"`) — values unchanged.

- [ ] **Step 1: Move source files**

```powershell
git mv src/AreWeDoomd.AgentService/Ai/Providers/OpenRouter src/AreWeDoomd.ChatProviders/Providers/OpenRouter
```

- [ ] **Step 2: Update namespaces in the 12 moved files**

- `OpenRouterProvider.cs`, `OpenRouterProviderOptions.cs`: namespace → `AreWeDoomd.ChatProviders.Providers.OpenRouter;`
- The 10 `Wire/` files: namespace → `AreWeDoomd.ChatProviders.Providers.OpenRouter.Wire;`
- In `OpenRouterProvider.cs`: fix `using ...OpenRouter.Wire;` → `using AreWeDoomd.ChatProviders.Providers.OpenRouter.Wire;`, delete the Task 2 `using AreWeDoomd.ChatProviders;` line.
- In `OpenRouterProviderOptions.cs`: delete the Task 2 `using AreWeDoomd.ChatProviders;` line.

- [ ] **Step 3: Fix the DI extensions using**

In `ChatProviderServiceCollectionExtensions.cs`: `using AreWeDoomd.AgentService.Ai.Providers.OpenRouter;` → `using AreWeDoomd.ChatProviders.Providers.OpenRouter;`.

- [ ] **Step 4: Move and fix the test file**

```powershell
git mv tests/AreWeDoomd.UnitTests/AgentService/Ai/OpenRouterProviderTests.cs tests/AreWeDoomd.UnitTests/ChatProviders/
```

Same using/namespace fixes as Task 3 Step 4, with `OpenRouter` in place of `Gemini`. The now-empty `tests/AreWeDoomd.UnitTests/AgentService/Ai/` folder disappears with the move (git tracks files, not folders); if an empty directory lingers on disk, delete it.

- [ ] **Step 5: Build and test**

Run: `dotnet build; dotnet test tests/AreWeDoomd.UnitTests`
Expected: build succeeds; full suite passes.

- [ ] **Step 6: Commit**

```powershell
git add -A; git commit -m "refactor: move OpenRouter adapter to AreWeDoomd.ChatProviders"
```

---

### Task 5: Move the DI extensions and finish the seam

**Files:**
- Move: `src/AreWeDoomd.AgentService/Ai/ChatProviderServiceCollectionExtensions.cs` → `src/AreWeDoomd.ChatProviders/ChatProviderServiceCollectionExtensions.cs`
- Modify: `src/AreWeDoomd.AgentService/Program.cs`

**Interfaces:**
- Consumes: everything moved in Tasks 2–4.
- Produces: `AreWeDoomd.ChatProviders.ChatProviderServiceCollectionExtensions.AddChatProviders(this IServiceCollection, IConfiguration)` — the single registration entry point Infrastructure will call in M6.

- [ ] **Step 1: Move the file**

```powershell
git mv src/AreWeDoomd.AgentService/Ai/ChatProviderServiceCollectionExtensions.cs src/AreWeDoomd.ChatProviders/
```

- [ ] **Step 2: Fix its namespace and usings**

- Namespace: `AreWeDoomd.AgentService.Ai` → `AreWeDoomd.ChatProviders`.
- Usings become exactly:

```csharp
using AreWeDoomd.ChatProviders.Providers.Gemini;
using AreWeDoomd.ChatProviders.Providers.OpenRouter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
```

- Also fix the stale XML doc example on the class (it says `GetRequiredKeyedService<IChatProvider>("anthropic")` — no such provider exists): change `"anthropic"` to `"openrouter"`.

- [ ] **Step 3: Fix Program.cs**

In `src/AreWeDoomd.AgentService/Program.cs`, the using block: keep `using AreWeDoomd.AgentService.Ai;` (still needed for `IAiSessionLogger`/`AiSessionLogger` at lines ~52) and ensure `using AreWeDoomd.ChatProviders;` is present (Task 2 added it — it now also resolves `AddChatProviders` and `IChatProvider`). No other change.

- [ ] **Step 4: Verify the leftover Ai/ folder contents**

Run: `Get-ChildItem -Recurse -File src/AreWeDoomd.AgentService/Ai | Select-Object -ExpandProperty Name`
Expected output: exactly `AiSessionLogger.cs` and `IAiSessionLogger.cs`.

- [ ] **Step 5: Build and test**

Run: `dotnet build; dotnet test tests/AreWeDoomd.UnitTests`
Expected: build succeeds; full suite passes.

- [ ] **Step 6: Commit**

```powershell
git add -A; git commit -m "refactor: move chat provider DI registration to AreWeDoomd.ChatProviders"
```

---

### Task 6: Final verification sweep

**Files:** none created; read-only checks plus any stragglers found.

**Interfaces:**
- Consumes: the finished library.
- Produces: green build/tests and proof no stale references remain — the M0 exit gate.

- [ ] **Step 1: Grep for stale namespace references**

Run: `git grep -n "AgentService.Ai.Providers"`
Expected: no matches. (Old provider namespaces must be gone everywhere, including tests.)

Run: `git grep -ln "AreWeDoomd.AgentService.Ai"`
Expected matches ONLY in: `src/AreWeDoomd.AgentService/Ai/AiSessionLogger.cs`, `src/AreWeDoomd.AgentService/Ai/IAiSessionLogger.cs`, `src/AreWeDoomd.AgentService/Program.cs`, `src/AreWeDoomd.AgentService/Processing/AgentEventProcessor.cs`, `tests/AreWeDoomd.UnitTests/AgentService/Processing/AgentEventProcessorTests.cs` (files that legitimately use `IAiSessionLogger`). Any other match is a missed edit — fix it.

- [ ] **Step 2: Full clean build + tests**

Run: `dotnet build --no-incremental; dotnet test`
Expected: 0 warnings introduced by the move (pre-existing warnings unchanged), all tests in both test projects pass.

- [ ] **Step 3: Config-compat spot check**

Run: `git grep -n "SectionName" src/AreWeDoomd.ChatProviders/`
Expected: `ChatProviders:Gemini` and `ChatProviders:OpenRouter` — unchanged, proving appsettings/.env keys still bind. Also confirm `appsettings.json` files were NOT touched: `git diff --stat HEAD~5 -- "*appsettings*"` → empty.

- [ ] **Step 4: Docker build check (compose users only; skip if docker unavailable)**

Run from `AreWeDoomd.Api/`: `docker build -f src/AreWeDoomd.AgentService/Dockerfile . -t awd-agent-smoke`
Expected: image builds (verifies the Task 1 Dockerfile COPY fix).

- [ ] **Step 5: Commit any straggler fixes**

Only if Steps 1–4 required changes:

```powershell
git add -A; git commit -m "refactor: finish ChatProviders extraction cleanup"
```

---

## Self-Review Notes

- **Spec coverage (§5):** moves list matches spec exactly (IChatProvider, request/result records, ChatProviderOptions, both provider folders, DI extensions); AiSessionLogger + prompting + decisions stay; provider selection stays config-keyed (`AgentServiceOptions.ChatProvider` → `GetRequiredKeyedService<IChatProvider>` in Program.cs is untouched); Infrastructure reference deliberately deferred to M6 per YAGNI.
- **Type consistency:** all new namespaces derive from `AreWeDoomd.ChatProviders` root; constants (`ProviderName`, `HttpClientName`, `SectionName`) explicitly pinned as unchanged in Tasks 3–4 Interfaces blocks.
- **Known judgment call:** wire DTO namespaces move with their providers (internal detail; nothing outside each provider file references them — verified by grep during planning).
