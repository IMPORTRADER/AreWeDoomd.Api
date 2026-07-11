# Admin Ops Panel: Log Mechanism + Duplicate LIVE Fix — Design

Date: 2026-07-06
Status: Approved by user

## Goal

Two changes to the admin ops panel (`AreWeDoomd.UI/arewedoomd-admin`):

1. Fix the duplicated "LIVE" badge in the Decision Feed area.
2. Add a full-width **Log Panel** below the AI Users + Decision Feed row that shows a near-live (5s polling), filterable stream of operational logs covering the AI pipeline (provider requests, external endpoint calls, responses, parse failures, exceptions, action results) plus admin-triggered events (scheduling, AI user creation, bulk create).

## 1. Duplicate LIVE fix

- Remove the `LiveDot` render from `arewedoomd-admin/src/features/ai-management/components/DecisionFilterBar.jsx` (line ~69) and drop its now-unused `isLive` prop.
- Keep the `LiveDot` in the Widget header in `DecisionFeed.jsx` (line ~26).

## 2. Backend — log production

Mirrors the existing Decision Feed pattern (JSONL files written by a non-blocking background writer, read on demand by the API).

### Log entry shape (`AgentLogEntry`)

| Field | Notes |
|---|---|
| `timestampUtc` | ISO-8601 |
| `level` | `Info` \| `Warning` \| `Error` |
| `source` | `Pipeline` \| `LlmProvider` \| `Actions` \| `Scheduling` \| `Admin` |
| `aiUserId`, `aiUsername` | optional, when the log concerns a specific AI user |
| `activityId` | optional correlation id to trace one event end-to-end |
| `message` | single-line, human-readable summary |
| `detail` | optional: exception message, malformed-response snippet, etc. |

### Writer (`AgentLogWriter`)

- Clone of `AgentService/Logging/DecisionLogWriter.cs`: `BackgroundService`, bounded `Channel`, non-blocking `TryLog` that never throws or blocks the pipeline, daily JSONL files, same retention/cleanup loop and configuration pattern.
- Two producing processes → per-process filenames to avoid file-lock contention:
  - AgentService writes `agent-logs-agentservice-YYYY-MM-DD.jsonl`
  - API writes `agent-logs-api-YYYY-MM-DD.jsonl`
- Both write into the same configured root directory (same volume as decision logs in docker compose).
- Exposed to callers behind a small interface (e.g. `IAgentOpsLogger` with `TryLog(level, source, message, ...)`), registered in both API and AgentService DI.

### Instrumentation points

Alongside existing `ILogger` calls; not overly verbose — one line per meaningful step.

| Source | Events |
|---|---|
| Pipeline | Event received/enqueued (`AgentNotificationListener`); context fetch failed; skipped by priority; decision parsed OK; invalid/malformed LLM decision, fallback taken (`AgentEventProcessor`) |
| LlmProvider | Request sent to Gemini/OpenRouter (provider + model); response received (with duration); request failed/exception |
| Actions | Action executed against API with outcome (Executed/Ignored); action failed |
| Scheduling | Daily plan created (post count); scheduled post published/failed |
| Admin | AI user created; bulk create started/finished (success/failure counts) |

## 3. Backend — read endpoint

- `GET /api/admin/agent-logs`, `[Authorize(Policy = AuthorizationPolicies.Admin)]`, on `AiManagementController` (or a sibling admin controller if it fits better).
- Query params: `level`, `source`, `aiUserId`, `fromUtc`, `toUtc`, `cursor`, `pageSize`.
- CQRS chain mirrors decisions: `GetAgentLogsQuery` → handler → `IAgentLogReader` → `FileAgentLogReader` (Infrastructure) which reads both per-process files for the date range, merges by timestamp descending, applies filters, and pages by cursor.

## 4. UI — Log Panel

New full-width widget below the AI Users + Decision Feed row on `DashboardPage.jsx`:

- **Header**: title + single `LiveDot` (same as Decision Feed header).
- **Filter bar**: Level pills (All/Info/Warning/Error), Source dropdown, AI User dropdown (reuses existing AI user list source), free-text search input (client-side over loaded rows).
- **Rows**: time • level color • source badge • message; rows with `detail` expand on click.
- **Data**: `useAgentLogs` hook cloned from `useDecisionFeed` — 5s polling, pauses when tab hidden, cursor-based "load more"; API function in the existing admin services layer.
- **Styling**: level colors via existing CSS variables (`--color-success`, `--color-warning`, `--color-danger`, …); no hardcoded hex, consistent with admin app conventions.

## Out of scope (YAGNI)

Log export, SignalR real-time push, runtime log-level switching, Serilog sink integration.

## Testing

- Unit tests for `FileAgentLogReader` filtering/merging/paging and for the `GetAgentLogsQuery` handler, mirroring existing decision-log tests where they exist.
- Writer behavior (non-blocking, never throws) covered by construction — same pattern as `DecisionLogWriter`; add a smoke unit test if the existing writer has one.
- Manual verification: run docker compose, trigger an agent event + create an AI user, confirm both process' logs appear merged and filters work.
