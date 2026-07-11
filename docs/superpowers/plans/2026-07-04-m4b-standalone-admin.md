# M4b: Standalone Admin Dashboard App — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract the AI Management dashboard out of the social SPA into its own Vite app (`AreWeDoomd.UI/arewedoomd-admin/`) with its own login (dedicated admin account, session fully independent of the social app), and strip every admin trace from `arewedoomd-ui`.

**Architecture:** New Vite React app beside the social one (same git repo, branch `doga/ai-dashboard`). Same backend, zero backend changes: the admin app logs in via `POST /api/auth/login` with a dedicated admin account, REQUIRES `isAdmin` in the response (non-admins get an error, no session), and stores its token under `adminAccessToken` (social app uses `accessToken` — the two sessions never touch, even on the same browser). The `features/ai-management` slice MOVES to the admin app; UI primitives it needs (Widget, Button, Input, Avatar, LiveDot, spinners, tokens/animations CSS, useCountUp/useDelayedLoading, needed icons, userType helpers) are COPIED — accepted duplication for isolation. The social app loses the AI Fleet tab, AdminRoute, AdminAiPage, and the feature folder.

**Tech Stack:** Vite 8 + React 19 + Tailwind 4 + axios + vitest (mirror arewedoomd-ui's package.json versions exactly). Port 5174 with the same `/api` dev proxy. No SignalR in the admin app.

## Global Constraints

- Repo `AreWeDoomd.UI`, branch `doga/ai-dashboard` (checked out). npm commands run from the app folder each task names. Commits plain, NO Co-Authored-By trailer; hook-blocked bash commit → PowerShell tool.
- Backend is UNTOUCHED in this milestone.
- The admin app keeps the SAME layering rules as the social app (pages → features → shared; API calls only in services; hooks own lifecycle; Widget for cards; `--color-*` tokens only) — copy `CLAUDE.md`-relevant rules into `arewedoomd-admin/README.md` so the convention travels with the app.
- Session separation invariant: the admin app must never read or write `localStorage['accessToken']`; the social app must never read `adminAccessToken`. Grep-verified in the final task.
- The moved `features/ai-management` code keeps its tests; the social app's test suite must stay green after the removal.
- After this milestone, M5/M6/M7 dashboard work targets `arewedoomd-admin` exclusively.

---

### Task 1: Scaffold arewedoomd-admin + copied UI kit

**Files (all new, under `AreWeDoomd.UI/arewedoomd-admin/`):**
- `package.json` (name `arewedoomd-admin`; same deps/devDeps + versions as arewedoomd-ui MINUS `@microsoft/signalr`; same scripts), `vite.config.js` (port **5174**, `/api` proxy identical to the social app's incl. `VITE_API_TARGET`; vitest test block identical), `index.html` (title "AreWeDoomd — Ops"), `eslint.config.js` (copy), `.env.example` (copy), `.gitignore` (copy).
- `src/main.jsx`, `src/App.jsx` (router mount), `src/index.css` — copy the social app's index.css WHOLESALE (tokens, animations, widget chrome, skeleton) — single source of the visual language.
- `src/components/ui/` — copy verbatim from arewedoomd-ui: `Widget.jsx`, `Button.jsx`, `Input.jsx`, `Avatar.jsx`, `LiveDot.jsx`, `Spinner.jsx`, `LoadingSpinner.jsx`, `userType.js` (+ their co-located tests where they exist).
- `src/components/icons/index.jsx` — copy ONLY the icons the dashboard uses (IconRobot + whatever the moved components import — check imports and trim).
- `src/hooks/` — copy `useCountUp.js`, `useDelayedLoading.js` (+ tests if present).
- `src/api/client.js` — same axios pattern BUT: token key `adminAccessToken`; 401 handler clears it and redirects to `/login` (the admin app's own login route).
- `src/test/setup.js` — copy.

**Steps:**
- [ ] Scaffold; `npm install` (from arewedoomd-admin/); paste/copy files.
- [ ] Smoke: a placeholder `App` renders "AreWeDoomd Ops"; `npm run dev` boots on 5174 (start + curl or just build); `npm run lint` + `npm run test` (copied component tests pass) + `npm run build` all green.
- [ ] Commit — `feat: scaffold standalone admin app with copied UI kit`

---

### Task 2: Admin auth (own context, login page, guarded router)

**Files (under `arewedoomd-admin/src/`):**
- `api/auth.js` — `adminAuthApi = { login: (username, password) => client.post('/api/auth/login', ...), me: () => client.get('/api/auth/me') }` (mirror the social app's auth service shape).
- `context/AdminAuthContext.jsx` — mirrors the social AuthContext pattern BUT: on login response, `if (!res.data.isAdmin) → clear token, throw error with message "Bu hesap admin yetkisine sahip değil."` (no session for non-admins); bootstrap via `me()` when `adminAccessToken` exists,同 isAdmin check; exposes `{ user, loading, login, logout }`.
- `pages/LoginPage/LoginPage.jsx` — centered `<Widget>` card: "AreWeDoomd Ops" heading, username+password `<Input>`s, error display (incl. the not-admin message), submit `<Button loading>`; on success navigate to `/`.
- `router/AppRouter.jsx` — `/login` public; everything else wrapped in a `RequireAdmin` guard (loading → LoadingSpinner; user → children; else Navigate `/login`); `/` → DashboardPage (placeholder until Task 3).
- Tests: `AdminAuthContext.test.jsx` (login stores token + sets user; **non-admin login → no token stored, error thrown**; logout clears) with mocked service; `RequireAdmin` redirect test.

**Steps:**
- [ ] Failing tests → RED; implement; lint+test green.
- [ ] Commit — `feat: add admin login with isolated session`

---

### Task 3: Move features/ai-management + AdminShell

**Files:**
- `git mv arewedoomd-ui/src/features/ai-management arewedoomd-admin/src/features/ai-management` (history-preserving), then fix the moved files' imports: `../../../api/client` still resolves (same depth) — VERIFY; ui-component imports now point at the admin app's copied `components/ui`; adjust any path drift.
- Move the feature's tests along (they live inside the folder) + `useAiUsers.test.jsx` etc. — confirm they run under the admin app's vitest.
- `src/components/layout/AdminShell.jsx` — lean top bar: "AreWeDoomd Ops" wordmark, nav placeholder (Dashboard active; future: Create, Jobs), right side = admin username + logout button. NOT the social 3-column shell — a simple `max-w-7xl mx-auto` content region.
- `src/pages/DashboardPage/DashboardPage.jsx` — the former AdminAiPage content (FleetStatsBar + grid with AiUserTable | DecisionFeed) verbatim, re-imported from the moved feature folder.

**Steps:**
- [ ] Move, fix imports, wire shell+page; admin app lint+test+build green (moved feature tests all pass here now).
- [ ] Commit — `feat: move AI management feature into the standalone admin app`

---

### Task 4: Strip admin from the social app

**Files (under `arewedoomd-ui/`):**
- Delete: `src/router/AdminRoute.jsx` (+test), `src/pages/AdminAiPage/`, the `/admin/ai` route in AppRouter, the AI Fleet NAV_ITEMS entry + `adminOnly` filter logic in AppShell, `IconRobot` (only if nothing else uses it — grep).
- KEEP: `isAdmin: Boolean(...)` normalization in AuthContext (harmless, still true data from the API) — but remove any UI usage.
- The `features/ai-management` folder is already gone (moved in Task 3).

**Steps:**
- [ ] Delete + fix references; `npm run lint` + `npm run test` + `npm run build` green in arewedoomd-ui (test count drops by the moved/deleted suites — expected; NO other failures).
- [ ] Commit — `refactor: remove in-app admin panel from the social app`

---

### Task 5: Final sweep (both apps)

- [ ] Session-separation grep: `grep -rn "adminAccessToken" arewedoomd-ui/src/` → ZERO; `grep -rn "'accessToken'" arewedoomd-admin/src/` → ZERO.
- [ ] `grep -rn "ai-management\|AdminRoute\|AdminAiPage\|AI Fleet" arewedoomd-ui/src/` → ZERO.
- [ ] Both apps: lint + test + build green (record counts).
- [ ] `arewedoomd-admin/README.md`: how to run (`npm run dev` → 5174, needs API on VITE_API_TARGET), the dedicated-admin-account setup (Admin:Usernames + restart + login), the layering rules copied from CLAUDE.md, note that UI-kit duplication is intentional.
- [ ] Update `AreWeDoomd.UI/CLAUDE.md`: one short section — repo now hosts TWO apps; admin work goes to arewedoomd-admin.
- [ ] Commit — `docs: document the standalone admin app`

---

## Self-Review Notes

- Operator decisions honored: separate Vite app (own port/deploy/login; zero admin code in the end-user bundle) + dedicated admin account over the unchanged `/api/auth/login` with `adminAccessToken` isolation; backend untouched.
- Forward tasks: M5 (CreateAiModal), M6 (BulkCreateModal + job polling), M7 polish all land in `arewedoomd-admin` — the AdminShell nav placeholder anticipates them.
- Risks called out: import-depth drift after the folder move (Task 3 verifies); social-app test count drop is expected and bounded to the moved/deleted suites; the not-admin login path is the one new security-relevant behavior and carries a dedicated test.
