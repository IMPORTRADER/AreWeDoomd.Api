# Chat Providers (AgentService)

## Overview

`AreWeDoomd.AgentService` calls an LLM through a keyed `IChatProvider`
abstraction (`src/AreWeDoomd.AgentService/Ai/IChatProvider.cs`). Two adapters
exist today: `GeminiProvider` (default) and `OpenRouterProvider`. Anthropic
has a config section reserved (`ChatProviders:Anthropic`) but no adapter yet.

Which one runs is a single config value:

```json
"AgentNotifications": { "ChatProvider": "gemini" }
```

Swapping providers never touches `AgentEventProcessor` or any consuming
code — the whole point of the keyed-DI pattern.

## Using OpenRouter

### 1. Get a key

Sign up at openrouter.ai → Keys → create one (`sk-or-v1-...`). Free-tier
models exist and need no card, but they're rate-limited and the lineup
changes over time. List what's currently free before picking one:

```bash
curl -s https://openrouter.ai/api/v1/models | jq -r '.data[] | select(.id | endswith(":free")) | .id'
```

### 2. Pick a model

OpenRouter model IDs are `provider/model-name`, optionally suffixed
`:free` — e.g. `openai/gpt-oss-20b:free`, `anthropic/claude-3.5-sonnet`,
`google/gemini-2.0-flash-001`. Goes in `AgentNotifications:Model`.

### 3. The three settings

| Setting | Key | Example |
|---|---|---|
| Provider | `AgentNotifications:ChatProvider` | `openrouter` |
| Model | `AgentNotifications:Model` | `openai/gpt-oss-20b:free` |
| API key | `ChatProviders:OpenRouter:ApiKey` | (your key) |

### 4. Setting it

**Local dev**, run from *inside* `src/AreWeDoomd.AgentService` (see Gotcha #1
below for why that directory matters):

```bash
cd src/AreWeDoomd.AgentService
OPENROUTER_API_KEY="sk-or-v1-..." \
AgentNotifications__ChatProvider="openrouter" \
AgentNotifications__Model="openai/gpt-oss-20b:free" \
dotnet run
```

`OPENROUTER_API_KEY` is special-cased in `Program.cs` — it's mapped
automatically onto `ChatProviders:OpenRouter:ApiKey`, so it doesn't need
the `Section__Key` double-underscore form. Everything else uses the
standard ASP.NET Core env var convention.

**Persistent local config** — `appsettings.Development.json` (gitignored,
create if missing) or `dotnet user-secrets`:

```json
{
  "AgentNotifications": { "ChatProvider": "openrouter", "Model": "openai/gpt-oss-20b:free" },
  "ChatProviders": { "OpenRouter": { "ApiKey": "sk-or-v1-..." } }
}
```

**Docker Compose** — `docker-compose.yml`'s `agent-service.environment`
already passes `OPENROUTER_API_KEY: ${OPENROUTER_API_KEY:-}` through from
`.env`. `AgentNotifications:ChatProvider` and `:Model` are not yet wired to
`.env` — they currently come from the checked-in `appsettings.json`
defaults (`gemini` / empty), so override them with additional
`environment:` entries in compose (or edit the checked-in defaults) to run
the containerized agent against OpenRouter.

### 5. Optional extras

`ChatProviders:OpenRouter:SiteUrl` / `SiteName` set the `HTTP-Referer`
/ `X-Title` headers OpenRouter uses for public model-ranking attribution —
cosmetic, safe to leave blank. `MaxRetries` / `RetryBaseDelayMs` control
retry behavior on 429/502/503/504 (defaults: 3 retries, 2000ms base delay,
doubling per attempt).

## Gotchas

**1. Run `dotnet run` from the project directory, not `--project` from the
repo root.** `dotnet run --project src/AreWeDoomd.AgentService` invoked from
the repo root can resolve the host's content root to the repo root instead
of the project directory, which makes `appsettings.json` silently fail to
load (`AddJsonFile(..., optional: true)` — no error, config values just
default to empty). Symptom: `OpenRouterProvider.BuildRequestUri()` throws
`UriFormatException: Invalid URI: The format of the URI could not be
determined` because `BaseUrl` bound to `""`. Fix: `cd
src/AreWeDoomd.AgentService && dotnet run`.

**2. Every registered provider's API key is required at startup, not just
the active one.** `AddChatProviders` (`Ai/ChatProviderServiceCollectionExtensions.cs`)
registers every implemented adapter with `.ValidateDataAnnotations().ValidateOnStart()`,
and `ValidateOnStart()` validates *all* such options eagerly at host
startup regardless of whether they're ever injected. So running with
`ChatProvider=openrouter` still requires `GEMINI_API_KEY` (or
`ChatProviders:Gemini:ApiKey`) to be some non-empty value — a placeholder
is fine, Gemini won't actually be called. This is deliberate, pre-existing
behavior (true for Gemini alone before OpenRouter existed), not a bug to
work around.

## Adding another provider

The registration method's own comment describes the recipe: one new
adapter file (clone `GeminiProvider` or `OpenRouterProvider` — own wire
DTOs in an `internal sealed record` `Wire/` subfolder, same retry/error-
normalization shape) + one call in `AddChatProviders`
(`Ai/ChatProviderServiceCollectionExtensions.cs`). Nothing else in
`AgentEventProcessor` or elsewhere needs to change.
