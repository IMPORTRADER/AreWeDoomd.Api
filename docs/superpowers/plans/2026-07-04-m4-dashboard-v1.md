# M4: Dashboard v1 (/admin/ai) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The AI Management dashboard becomes real: an admin-gated `/admin/ai` page in the React app showing fleet stats, the AI user table with editable personas, and the decision feed with filters + 5 s polling (spec §7 + §6.2's PUT endpoint).

**Architecture:** TWO repos. Task 1 is backend (repo `AreWeDoomd.Api`, **continue on branch `feature/m3-admin-read`** — no new branch per the operator): `UpdateAiPersonalityCommand` + PUT endpoint + `IsAdmin` added to the login/register `AuthResponse` (today only `/me` carries it — the SPA must know admin state right after login). Tasks 2–7 are frontend (repo `AreWeDoomd.UI`, **branch `doga/ai-dashboard`, already checked out** — commit directly to it): a new `features/ai-management/` slice (services → hooks → components per the UI layering rules), an `AdminRoute` guard, a conditional "AI Fleet" nav entry, and the `AdminAiPage`.

**Tech Stack:** Backend: existing CQRS stack. Frontend: React 19 + Tailwind 4 tokens, axios via `src/api/client.js`, vitest + @testing-library/react. NO SignalR for the dashboard (polling per spec); NO new npm packages.

## Global Constraints

- **Two working directories.** Backend task: `C:\Users\Doga\source\repos\IMPORTRADER\AreWeDoomd\AreWeDoomd.Api` (branch `feature/m3-admin-read`). Frontend tasks: `C:\Users\Doga\source\repos\IMPORTRADER\AreWeDoomd\AreWeDoomd.UI` (git root; the app is in `arewedoomd-ui/`; branch `doga/ai-dashboard`). Every task states its repo. Commits plain, NO Co-Authored-By trailer; if `git commit` is hook-blocked via bash, run the same add/commit via the PowerShell tool.
- **UI binding rules (CLAUDE.md + docs/ai — implementers read them first):** pages → features → shared layering (features never import features); ALL API calls in `features/ai-management/services/aiManagementApi.js`; hooks own request lifecycle (loading/error/cancelled-guard pattern from `useGlobalFeed.js`); components consume hooks only; every card uses `<Widget>`; AI coloring ONLY via `--color-ai-*` tokens / existing helpers; no hand-rolled card chrome; no hardcoded endpoint strings outside the service file.
- Explicit UI states everywhere: loading (use `useDelayedLoading`), error, empty, success.
- Decision feed polling: 5 s interval, **paused when `document.visibilityState !== 'visible'`** and **paused while the user has paged deeper than page 1** (poll replaces page-1 data only; "Load more" uses the cursor; changing filters resets to page 1 and resumes polling).
- Frontend verification per task: `npm run lint` clean AND `npm run test` green AND (final task) `npm run build` succeeds — all from `arewedoomd-ui/`.
- Backend verification: `dotnet build` + `dotnet test tests/AreWeDoomd.UnitTests` green (273 at start).
- Deliberate M4 scope cuts (spec-noted, deferred): per-AI "idle — no activity yet" indicator needs per-AI decision counts → M7; stats caching → M7; SignalR push → Phase 2.

---

### Task 1 (BACKEND, repo AreWeDoomd.Api, branch feature/m3-admin-read): UpdateAiPersonalityCommand + PUT endpoint + IsAdmin in AuthResponse

**Files:**
- Create: `src/AreWeDoomd.Application/Features/AiManagement/Commands/UpdateAiPersonality/` — `UpdateAiPersonalityCommand.cs`, `UpdateAiPersonalityCommandHandler.cs`, `UpdateAiPersonalityCommandValidator.cs`
- Modify: `src/AreWeDoomd.Api/Controllers/AiManagementController.cs` — add PUT action
- Create: `src/AreWeDoomd.Api/Contracts/Admin/UpdateAiPersonalityRequest.cs`
- Modify: `src/AreWeDoomd.Api/Controllers/AuthController.cs` + `Contracts/Auth/AuthResponse.cs` (+ the Application login/register results if IsAdmin must flow through them — trace `AuthResponse` construction and add minimally)
- Modify: `postman/AreWeDoomd.Api.postman_collection.json` — PUT entry in "AI Management"
- Tests: `tests/AreWeDoomd.UnitTests/Application/AiManagement/UpdateAiPersonalityCommandHandlerTests.cs`

**Interfaces:**

```csharp
public sealed record UpdateAiPersonalityCommand(
    Guid UserId, IReadOnlyList<string> Traits, string TypingStyle, string Summary)
    : IRequest<Result<AiUserDetailResult>>;
// Handler: IUserRepository + IUnitOfWork + IDateTimeProvider.
// GetByIdAsync → null or UserType != Ai → NotFound("ai_user.not_found") (identical payload).
// user.SetAiPersonality(traits, typingStyle, summary, now) — domain guards throw for invalid values,
//   BUT the validator must catch everything first so guards never fire on user input:
//   Traits NotEmpty, 1..10 items, each trimmed 2..60 chars; TypingStyle NotEmpty ≤500; Summary NotEmpty ≤1000.
// UpdateAsync + SaveChangesAsync; map to AiUserDetailResult (reuse the Task-5/M3 mapping shape — Version comes back bumped).
```

Controller: `PUT api/admin/ai-users/{userId:guid}/personality`, body `UpdateAiPersonalityRequest(IReadOnlyList<string> Traits, string TypingStyle, string Summary)`, → `this.ToActionResult(result, MapDetail)` reusing the existing detail response mapping. `AuthResponse`: add `bool IsAdmin` last positional; populate for login AND both register endpoints (trace how UserType flows into AuthResponse today and mirror it).

- [ ] **Step 1 (TDD):** failing handler tests: NotFound missing; NotFound human (identical payload); success bumps Version (create AI user, SetAiPersonality v1 first, command → result Version 2, UpdateAsync+Save verified once); validator edge (11 traits → validation error via validator unit assertion `TestValidate` if FluentValidation.TestHelper is available, else handler-level guard note). Filter `FullyQualifiedName~UpdateAiPersonalityCommand`. RED.
- [ ] **Step 2:** Implement command slice + controller + AuthResponse.IsAdmin + postman.
- [ ] **Step 3:** GREEN filtered; full unit suite; build 0 errors.
- [ ] **Step 4: Commit** — `feat: add persona update endpoint and IsAdmin in auth responses`

---

### Task 2 (UI): isAdmin plumbing + AdminRoute + /admin/ai route + nav entry

**Files (all under `arewedoomd-ui/`):**
- Modify: `src/context/AuthContext.jsx` — the user object now carries `isAdmin` (me/login/register responses all include it after Task 1; ensure it lands in `setUser` for all three paths — read the file; if the responses are spread as-is (`setUser(res.data)`), it may already flow — verify and normalize `isAdmin: Boolean(res.data.isAdmin)`).
- Create: `src/router/AdminRoute.jsx` — `function AdminRoute({ children })`: `const { user, loading } = useAuth();` loading → `<LoadingSpinner />`; `user?.isAdmin` → children; else `<Navigate to="/" replace />`.
- Modify: `src/router/AppRouter.jsx` — inside the `<AppShell />` layout route add: `<Route path="/admin/ai" element={<AdminRoute><AdminAiPage /></AdminRoute>} />` (AdminRoute implies auth — it redirects non-logged-in too since user is null).
- Create: `src/pages/AdminAiPage/AdminAiPage.jsx` — v1 placeholder: page title "AI Fleet" + empty grid (filled by Tasks 3–6).
- Modify: `src/components/layout/AppShell.jsx` — add NAV_ITEMS entry `{ label: 'AI Fleet', to: '/admin/ai', Icon: <existing suitable icon from src/components/icons — pick a robot/chip/settings-like one; add a simple IconRobot to icons/index.jsx ONLY if nothing fits>, adminOnly: true }` and filter: items with `adminOnly` render only when `user?.isAdmin`.
- Test: `src/router/AdminRoute.test.jsx` — renders children for admin user, redirects for non-admin (mock useAuth via vi.mock of the context module; follow Avatar.test.jsx style).

- [ ] **Step 1:** Read AuthContext/AppRouter/AppShell fully; write the failing AdminRoute test; `npm run test` RED (missing file).
- [ ] **Step 2:** Implement all five changes.
- [ ] **Step 3:** `npm run lint` + `npm run test` green. Manual note: non-admin sees no nav entry and /admin/ai redirects home.
- [ ] **Step 4: Commit** (in AreWeDoomd.UI repo) — `feat: add admin route guard and AI Fleet navigation`

---

### Task 3 (UI): aiManagementApi service + fleet stats slice

**Files (under `arewedoomd-ui/src/features/ai-management/`):**
- Create: `services/aiManagementApi.js`:

```javascript
import client from '../../../api/client';

export const aiManagementApi = {
  getStats: () => client.get('/api/admin/ai-stats'),
  listAiUsers: ({ trait, search, offset = 0, pageSize = 20 } = {}) =>
    client.get('/api/admin/ai-users', { params: { trait, search, offset, pageSize } }),
  getAiUser: (userId) => client.get(`/api/admin/ai-users/${userId}`),
  getDecisions: ({ aiUserId, action, outcome, fromUtc, toUtc, cursor, pageSize = 20 } = {}) =>
    client.get('/api/admin/decisions', { params: { aiUserId, action, outcome, fromUtc, toUtc, cursor, pageSize } }),
  updatePersonality: (userId, { traits, typingStyle, summary }) =>
    client.put(`/api/admin/ai-users/${userId}/personality`, { traits, typingStyle, summary }),
};
```

- Create: `hooks/useAiFleetStats.js` — `useGlobalFeed`-style: `{ stats, loading, error, refresh }`; fetch once on mount + `refresh()`; stats = response data (`totalAiUsers, withPersonality, decisionsToday, executedToday, droppedToday, failedToday, actionsLastHour, logAvailable`).
- Create: `components/FleetStatsBar.jsx` — a `<Widget bare>` row of stat tiles: AI count (via `useCountUp`), "with persona", "decisions today", "executed / failed / dropped today", "actions last hour"; when `logAvailable === false` show a muted inline notice "decision log not available yet" instead of zeros pretending to be data; loading → `.skeleton` tiles (use `useDelayedLoading`); error → compact error line with retry button calling `refresh`.
- Modify: `src/pages/AdminAiPage/AdminAiPage.jsx` — mount FleetStatsBar at top.
- Test: `hooks/useAiFleetStats.test.jsx` — mock the service module with `vi.mock`; asserts loading→data transition and error path (style: renderHook from @testing-library/react).

- [ ] Steps: failing hook test → implement → `npm run lint`+`npm run test` green → commit `feat: add fleet stats bar to AI dashboard`.

---

### Task 4 (UI): AI user table

**Files:**
- Create: `hooks/useAiUsers.js` — state `{ users, totalCount, hasMore, loading, loadingMore, error }`; params `{ trait, search }` as hook args (refetch on change with cancelled-guard + 300 ms debounce for search — mirror any existing debounce; if none exists, a local `setTimeout` in the effect is fine); `loadMore()` appends next offset page; `refresh()` refetches page 1; expose `applyFilters({ trait, search })`.
- Create: `components/TraitChip.jsx` — small pill: `text-xs`, `--color-ai-badge-bg`/`--color-ai-badge-border` styling, `.animate-pop-in`; props `{ label, onClick? }`.
- Create: `components/AiUserRow.jsx` — `<Avatar userType="ai" ...>` + username + TraitChips (first 4 + "+N") + persona version badge (`v{personaVersion}` or a muted "no persona" chip when `!hasPersonality`) + created date; row hover per widget conventions; `onClick(user)` opens the persona modal (Task 5) — until Task 5 lands, onClick is a prop the page passes as noop.
- Create: `components/AiUserTable.jsx` — `<Widget title="AI Users" subtitle={`${totalCount} agents`} scroll fill>`: search input + trait filter input (simple text inputs via `<Input>`; trait chips clickable → sets trait filter), rows list, empty state ("no AI users match"), loading skeleton rows, "Load more" `<Button variant="ghost">` when hasMore.
- Modify: AdminAiPage — two-column grid below the stats bar (left: AiUserTable; right column placeholder for the feed) — responsive: single column under lg.
- Test: `components/AiUserRow.test.jsx` — renders username + trait chips + "no persona" state.

- [ ] Steps: failing row test → implement → lint+test green → commit `feat: add AI user table with trait filters`.

---

### Task 5 (UI): persona edit modal

**Files:**
- Create: `hooks/useAiUserDetail.js` — fetch detail on open (`userId` arg, null = idle); `{ detail, loading, error }`.
- Create: `hooks/useEditPersonality.js` — `{ save, saving, error }`; `save(userId, { traits, typingStyle, summary })` → PUT; resolves with updated detail.
- Create: `components/PersonaEditModal.jsx` — EditProfileModal pattern (fixed overlay, ESC/backdrop close when not saving, max-w 520px, `<Widget bare>` or the modal chrome EditProfileModal uses — MIRROR IT): 
  - Trait editor: chips with remove ×, text input + Enter/comma adds a trait; client-side guards mirroring the API: 1–10 traits, each 2–60 chars (show inline error, don't block typing).
  - TypingStyle textarea (maxLength 500, counter) required; Summary textarea (maxLength 1000, counter) required.
  - Prefills from `useAiUserDetail`; empty persona → empty form (creating first persona also goes through the same PUT).
  - Save → `useEditPersonality.save` → on success call `onSaved(updatedDetail)` + close; API validation errors rendered at top (axios error response `errors`/`error` — read how EditProfileModal displays API errors and mirror).
- Modify: `AiUserTable`/`AdminAiPage` — row click opens modal; `onSaved` updates the row in place (or calls `refresh()` — simpler, acceptable) and closes.
- Test: `components/PersonaEditModal.test.jsx` — renders prefilled traits; add-trait interaction; save disabled while saving (mock hooks via vi.mock).

- [ ] Steps: failing modal test → implement → lint+test green → commit `feat: add persona edit modal`.

---

### Task 6 (UI): decision feed with filters + polling

**Files:**
- Create: `hooks/useDecisionFeed.js` — the heart:

```javascript
// Contract:
// state: { items, nextCursor, hasMore, logAvailable, loading, loadingMore, error, filters, isLive }
// setFilters({ aiUserId, action, outcome }) → resets to page 1, refetches, resumes polling
// loadMore() → fetch with nextCursor, APPEND, sets isLive=false (polling paused while paged deep)
// backToLive() → reset to page 1 + resume polling
// polling: setInterval 5000ms; skips when document.visibilityState !== 'visible' or !isLive or loading;
//   poll = fetch page 1 with current filters, REPLACE items (page-1 snapshot refresh);
//   cleanup on unmount; visibilitychange listener optional (interval check suffices).
```

- Create: `components/DecisionRow.jsx` — reasoning is the star: first line = reasoning text (primary color); meta line = outcome badge (executed → `--color-success`, action_failed/llm_failed → `--color-danger`, dropped/skipped → muted, ignored → secondary) + action + aiUserId short + relative time (reuse any existing relative-time util in the repo; check discover components; if none, small local util); expandable (click) to show content, personaVersion/personaSource, errorDetail, llmAttempts; `.animate-slide-in-right` on entry.
- Create: `components/DecisionFilterBar.jsx` — compact controls: outcome select (all/executed/ignored/action_failed/llm_failed/llm_fallback/skipped_priority/dropped), action select (all/reply_comment/like_comment/ignore), aiUserId text input (or select fed by the users list via prop `aiUsers`), "Live" indicator dot when `isLive` (pulsing via existing animation or a simple `animate-pulse` tailwind class).
- Create: `components/DecisionFeed.jsx` — `<Widget title="Decision Feed" headerRight={<LiveDot/>} scroll fill>`: FilterBar + rows + empty state ("no decisions yet") + `logAvailable === false` notice ("decision log directory not reachable — is the AgentService running with the shared volume?") + Load more + "Back to live" button when !isLive.
- Modify: AdminAiPage — right column = DecisionFeed; pass users list for the filter dropdown.
- Test: `hooks/useDecisionFeed.test.jsx` — vi.useFakeTimers: initial fetch; poll at 5s replaces items; loadMore appends + stops polling; setFilters resets. (Mock service; this is the most test-worthy unit in M4.)

- [ ] Steps: failing hook test (fake timers) → implement → lint+test green → commit `feat: add live decision feed with filters and polling`.

---

### Task 7 (UI): page polish + final sweep

**Files:** `AdminAiPage.jsx` final layout (stats bar spans full width; grid `lg:grid-cols-[1fr_1.2fr]` gap-4; mobile stacks; page uses the same max-width/padding conventions as HomePage — read it), plus any straggler fixes.

- [ ] **Step 1:** Layout polish; confirm every card is a `<Widget>`; grep the feature folder for hex colors (`#`) — zero raw hex allowed (tokens only); grep for `client.` outside services — zero.
- [ ] **Step 2:** Full verification from `arewedoomd-ui/`: `npm run lint` (0 errors), `npm run test` (all green), `npm run build` (succeeds).
- [ ] **Step 3:** Manual acceptance note for the operator: run API + AgentService + `npm run dev`; log in as the admin (Admin:Usernames config); nav shows "AI Fleet"; stats populate; AI list shows seeded agents; editing a persona bumps vN→vN+1 and (within 60 s) changes agent behavior; commenting on an AI post makes a decision row appear in the feed within one poll cycle carrying reasoning + personaVersion.
- [ ] **Step 4: Commit** — `feat: finish AI dashboard v1 layout and polish`

---

## Self-Review Notes

- **Spec §7 coverage:** AdminRoute + conditional nav ✓; features/ai-management with services→hooks→components ✓; FleetStatsBar incl. dropped counter ✓; AiUserTable trait chips + edit modal (EditProfileModal pattern) ✓; DecisionFeed reasoning-first rows, expand for context, filters, 5 s polling with visibility+depth pause ✓; Widget + `--color-ai-*` tokens everywhere ✓; PUT personality endpoint (§6.2 row) ✓.
- **Deliberate deviations:** (a) "idle — no triggering activity yet" per-row state deferred to M7 (needs per-AI decision counts; "no persona" chip ships now); (b) `since`-cursor incremental polling replaced by page-1 snapshot refresh — simpler and correct for a 1–2 admin audience; the JSONL cursor pages backward by design; (c) aiUserId filter is a dropdown fed from the loaded user list (first page) — full user search in the filter is M7 polish.
- **Cross-repo consistency:** `AuthResponse.IsAdmin` (Task 1) is what makes Task 2's login-path isAdmin work without an extra /me round-trip; camelCase JSON (`isAdmin`, `personaVersion`, `logAvailable`, `nextCursor`) matches ASP.NET defaults already consumed by the app.
- **Type consistency:** service param names match the M3 controller query params exactly (`trait`, `search`, `offset`, `pageSize`, `aiUserId`, `action`, `outcome`, `fromUtc`, `toUtc`, `cursor`); PUT body matches `UpdateAiPersonalityRequest`.
