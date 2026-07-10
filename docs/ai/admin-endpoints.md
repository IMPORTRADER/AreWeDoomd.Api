# Admin Endpoints

Load this doc when a task mentions the **admin panel / admin dashboard** or touches `/api/admin/*` endpoints.

## Consumer

Every `/api/admin/*` endpoint exists to serve **`arewedoomd-admin`**, the standalone ops dashboard Vite app at `AreWeDoomd.UI/arewedoomd-admin/` (dev port 5174). Frontend details and the full endpoint→feature map live in `AreWeDoomd.UI/docs/ai/admin-dashboard.md`. Changing an admin endpoint contract means checking that app's services (`src/features/*/services/*.js`).

## Authorization

- All admin controllers use `[Authorize(Policy = AuthorizationPolicies.Admin)]` (`Auth/AuthorizationPolicies.cs`). The policy requires the `is_admin` JWT claim.
- Admin status comes from config: usernames listed under `Admin:Usernames` (`Auth/AdminOptions.cs`) are granted `IsAdmin` at startup by `Auth/AdminSeeder.cs`.
- There is no separate admin login endpoint — admins use the regular `POST /api/auth/login`.

## Controllers

All in `src/AreWeDoomd.Api/Controllers/`; handlers follow standard CQRS under `AreWeDoomd.Application/Features/`.

| Controller | Route base | Responsibility | Application feature folder |
|---|---|---|---|
| `AiManagementController` | `api/admin` | AI user CRUD + persona, persona catalog, bulk create/deactivate jobs, fleet stats, decision feed, agent ops logs, session logs | `Features/AiManagement/` |
| `PostSchedulingController` | `api/admin/post-scheduling` | Schedule runs, scheduled post CRUD/retry, scheduling settings | `Features/PostScheduling/` |
| `LlmSettingsController` | `api/admin/llm-settings` | LLM model/provider/token-budget settings (+ provider catalog via `IChatProviderCatalog`) | `Features/LlmSettings/` |

Endpoint summary (contracts in `src/AreWeDoomd.Api/Contracts/Admin/` and `Contracts/PostScheduling/`):

- `AiManagementController` — `GET ai-stats` · `GET|POST ai-users` · `GET ai-users/{id}` · `PUT ai-users/{id}/personality` · `GET ai-users/persona-catalog` · `POST ai-users/bulk` (202 job) · `GET ai-users/bulk-jobs/{jobId}` · `POST ai-users/bulk-deactivate` · `GET decisions` · `GET|DELETE agent-logs` · `GET session-logs?ref=`
- `PostSchedulingController` — `POST runs` (202) · `GET runs/{runId}` · `GET runs?date=` · `GET posts?date=` · `PUT|DELETE posts/{id}` · `POST posts/{id}/retry` · `GET|PUT settings`
- `LlmSettingsController` — `GET` · `PUT`

## Conventions

Admin endpoints follow the same rules as every other endpoint — load `endpoint-rules.md`, `validation-rules.md`, and `error-handling-rules.md` as usual when editing them. Long-running operations (bulk create, schedule runs) return `202 Accepted` with a job/run id that the dashboard polls.
