# M5: Custom AI Creation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Admin creates a single AI user with hand-picked traits from the standalone dashboard: `POST /api/admin/ai-users` (account + personality in ONE transaction, random internal password) + a CreateAiModal in `arewedoomd-admin` (spec §6.2 custom-create row).

**Architecture:** Backend extracts `IAiAccountFactory` from the registration flow (normalization + uniqueness checks + `User.Create`, NO SaveChanges — the caller owns the transaction); `RegisterUserCommandHandler` is refactored onto it (single source of account-creation truth) and `CreateAiUserCommand` composes factory + `SetAiPersonality` + one `SaveChangesAsync`. M6's bulk creation will be the factory's third consumer. Frontend adds a "New AI" button on the AI Users widget opening CreateAiModal (PersonaEditModal's sibling: username/email inputs + the same trait/typing/summary editor).

**Tech Stack:** existing stacks; no new packages either side.

## Global Constraints

- Backend: repo `AreWeDoomd.Api`, EXISTING branch `feature/m3-admin-read` (user's standing instruction: no new branches). **`src/AreWeDoomd.Api/appsettings.json` carries the operator's uncommitted local edit (Admin:Usernames) — implementers must NOT touch, stage, or commit that file; use `git add` with explicit paths, never `git add -A`.** Frontend: repo `AreWeDoomd.UI`, branch `doga/ai-dashboard`, app `arewedoomd-admin/` ONLY.
- Commits plain, NO Co-Authored-By trailer; hook-blocked bash commit → PowerShell tool.
- One transaction invariant: a created AI user row must never exist without its personality row (SetAiPersonality before the single SaveChanges).
- Password: random, ≥32 chars (e.g. two GUID "N" strings concatenated), hashed via IPasswordHasher, NEVER returned in any response or log.
- Email optional in the API: absent/blank → synthesize `{normalizedUsername}@ai.arewedoomd.local` (lowercase). Validator: when provided, must be a valid email.
- Username/trait/typing/summary validation mirrors existing validators: username rules copied from `RegisterUserCommandValidator` (read it — mirror exactly); persona rules identical to `UpdateAiPersonalityCommandValidator` (1–10 traits, each trimmed 2–60, typingStyle/summary required non-whitespace with 500/1000 caps).
- Backend verify per task: `dotnet build` + `dotnet test tests/AreWeDoomd.UnitTests` green (279 at start). Frontend verify: `npm run lint` 0 errors + `npm run test` green from `arewedoomd-admin/` (56 at start).

---

### Task 1 (BACKEND): IAiAccountFactory + CreateAiUserCommand + POST endpoint

**Files:**
- Create: `src/AreWeDoomd.Application/Common/Interfaces/IAiAccountFactory.cs`
- Create: `src/AreWeDoomd.Application/Common/Services/AiAccountFactory.cs` (plain Application-layer class — deps are all Application interfaces)
- Modify: `src/AreWeDoomd.Application/Features/Authentication/Commands/RegisterUser/RegisterUserCommandHandler.cs` — refactor onto the factory (behavior identical; existing register tests must stay green UNCHANGED except DI/ctor mechanics)
- Create: `src/AreWeDoomd.Application/Features/AiManagement/Commands/CreateAiUser/` — `CreateAiUserCommand.cs`, `CreateAiUserCommandHandler.cs`, `CreateAiUserCommandValidator.cs`
- Modify: `src/AreWeDoomd.Api/Controllers/AiManagementController.cs` — `POST api/admin/ai-users`
- Create: `src/AreWeDoomd.Api/Contracts/Admin/CreateAiUserRequest.cs`
- Modify: wherever Application services are DI-registered (find how existing Application/Common services register — mirror; if none exist there, register in the Api's DI alongside other Application wiring)
- Modify: `postman/AreWeDoomd.Api.postman_collection.json` — POST entry
- Tests: `tests/AreWeDoomd.UnitTests/Application/Common/AiAccountFactoryTests.cs` + `tests/AreWeDoomd.UnitTests/Application/AiManagement/CreateAiUserCommandHandlerTests.cs`

**Interfaces:**

```csharp
public interface IAiAccountFactory
{
    /// <summary>Normalizes, checks uniqueness, creates the User (UserType.Ai) and stages it via AddAsync.
    /// Does NOT SaveChanges — the caller owns the transaction. Password arrives pre-generated, is hashed here.</summary>
    Task<Result<User>> CreateAiAccountAsync(
        string username, string email, string password, DateTimeOffset now, CancellationToken ct);
    // Conflict("auth.username_taken") / Conflict("auth.email_taken") exactly as the register flow returns today.
}

public sealed record CreateAiUserCommand(
    string Username, string? Email, IReadOnlyList<string> Traits, string TypingStyle, string Summary)
    : IRequest<Result<AiUserDetailResult>>;
// Handler: email = string.IsNullOrWhiteSpace(Email) ? $"{normalizedUsername.ToLowerInvariant()}@ai.arewedoomd.local" : Email;
// password = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
// factory.CreateAiAccountAsync(...) → failure passes through; success → user.SetAiPersonality(Traits, TypingStyle, Summary, now)
// → ONE unitOfWork.SaveChangesAsync → map AiUserDetailResult (same mapping shape as UpdateAiPersonality handler).
```

`RegisterUserCommandHandler` refactor: it currently normalizes → two taken-checks → User.Create → AddAsync → Save → AuthResult. Generalize the factory to `IAccountFactory`? NO — keep scope tight: the factory is AI-specific per the spec name; the register handler keeps its own human/AI generic path BUT to avoid duplicated uniqueness logic, extract the SHARED core as the factory method with a `UserType` parameter internally? Resolution (binding): name stays `IAiAccountFactory` with the AI-specific signature above; internally `AiAccountFactory` and `RegisterUserCommandHandler` may share a private static helper ONLY if trivially clean — otherwise accept the register handler keeping its 15 lines and the factory being the canonical path for ADMIN-created AI accounts (document the choice in the report; reviewer adjudicates). What must NOT happen: behavior change in registration.

- [ ] **Step 1 (TDD):** failing tests — factory: username-taken → Conflict auth.username_taken + AddAsync never; email-taken → Conflict; success → AddAsync once with UserType.Ai + hashed password (verify passwordHasher.Hash called with the given password, user gets the HASH); SaveChanges NEVER called by the factory. Handler: conflict passthrough (no SaveChanges); success → personality set (detail result has traits/version 1), ONE SaveChanges, synthetic email when Email null (assert result/user email `{username}@ai.arewedoomd.local`); explicit email used when provided. Validator: bad username / 11 traits / whitespace summary rejected (mirror UpdateAiPersonality validator test style). RED.
- [ ] **Step 2:** Implement + DI + controller + postman.
- [ ] **Step 3:** GREEN filtered; FULL unit suite green (register tests untouched-green); build 0 errors.
- [ ] **Step 4: Commit** (explicit paths — never `git add -A`; appsettings.json stays untouched) — `feat: add custom AI creation with shared account factory`

---

### Task 2 (UI, arewedoomd-admin): CreateAiModal + wiring

**Files (under `arewedoomd-admin/src/features/ai-management/`):**
- Modify: `services/aiManagementApi.js` — add `createAiUser: ({ username, email, traits, typingStyle, summary }) => client.post('/api/admin/ai-users', { username, email: email || undefined, traits, typingStyle, summary })`.
- Create: `hooks/useCreateAiUser.js` — `{ create, creating, error }`; axios errors captured + rethrown (PersonaEditModal's useEditPersonality pattern).
- Create: `components/CreateAiModal.jsx` — PersonaEditModal's chrome/pattern (read it first; extract-shared is NOT required — sibling file is fine): fields = username `<Input>` (required, 3–24, live inline error), email `<Input>` (optional — helper text "Boş bırakılırsa otomatik üretilir"), then the SAME trait-chip editor + typingStyle/summary textareas with counters (copy the editor JSX from PersonaEditModal; if trivially extractable into a shared `PersonaFormFields.jsx` without prop sprawl, do it and use it in BOTH modals — implementer's call, stated in report). Save → create() → onCreated(detail) + close; 409 conflict errors (username/email taken) surfaced at top.
- Modify: `components/AiUserTable.jsx` — `headerRight` gains a "New AI" `<Button size="sm">` (prop `onCreateClick`).
- Modify: `pages/DashboardPage/DashboardPage.jsx` — createOpen state; onCreated → refresh list + close.
- Tests: `components/CreateAiModal.test.jsx` — renders fields; username validation error inline; save disabled until valid; conflict error surfaced (mock hooks).

- [ ] Steps: failing modal tests → RED → implement → `npm run lint` + `npm run test` green → commit — `feat: add create AI modal to the admin dashboard`

---

### Task 3: Final sweep

- [ ] Backend: `dotnet build --no-incremental` 0 errors; `dotnet test` both projects (only the known pre-existing IntegrationTests failure); grep: password never in responses/logs — `git grep -n "password" src/AreWeDoomd.Application/Features/AiManagement/ src/AreWeDoomd.Api/Contracts/Admin/` → only the internal generation/hash sites, nothing in DTOs.
- [ ] Frontend: lint/test/build green from arewedoomd-admin (record counts).
- [ ] Manual acceptance note: dashboard → "New AI" → create `test-bot` with traits → appears in the table with v1 persona; AgentService picks it up on its first triggering event without restart (M2's lazy persona fetch); duplicate username → clean 409 message in the modal.
- [ ] Commit stragglers if any — `chore: finish M5 verification`

---

## Self-Review Notes

- Spec §6.2 custom-create row: random internal password ✓ never exposed ✓; IAiAccountFactory extraction ✓ (M6-ready); account+personality single transaction ✓; synthetic email default ✓ (M6 parity); 409 conflicts surfaced ✓; admin policy inherited from controller class attribute ✓.
- Deliberate scope: no per-field uniqueness pre-check endpoint (409 on submit is fine at this scale); modal does not offer avatar/biography (persona-only creation; profile fields default) — spec silent, kept minimal.
- Risk flagged: RegisterUserCommandHandler refactor must not change registration behavior — its existing tests are the guard; the factory-vs-shared-helper decision is explicitly delegated with reviewer adjudication.
- appsettings.json local-edit hazard called out in Global Constraints (explicit-path staging only).
