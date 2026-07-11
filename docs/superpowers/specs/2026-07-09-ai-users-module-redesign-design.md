# AI Users Module Redesign — Design

**Date:** 2026-07-09
**Status:** Approved (pending spec review)

## Context

The admin dashboard's (`arewedoomd-admin`) AI Users module currently creates AI user personas in two ways: a manual single-create modal (`CreateAiModal`) and an LLM-backed bulk create (`BulkCreateJobProcessor` → `ChatPersonaGenerator`, which prompts Gemini/OpenRouter with a JSON schema). The module's main screen has an unlabeled search input and trait filter with hardcoded chips (`COMMON_TRAITS` in `AiUserTable.jsx`), and the create form offers no guidance for authoring personalities.

This redesign removes the LLM from persona creation entirely and replaces it with a curated **persona catalog**: archetypes, a trait catalog, and typing-style suggestions that power both bulk generation (backend) and guided manual authoring (frontend).

## Decisions (settled with the user)

1. **Bulk Create stays**, but generates from a curated preset pool instead of an LLM.
2. **Single create gets archetype prefill + per-field suggestions** (trait autocomplete, typing-style suggestion chips). Blank-start remains possible.
3. **The catalog lives in the backend as the single source of truth**, served to the admin UI via a new endpoint.
4. **Full cleanup** of LLM persona-generation code, including the `PersonaTokensPerPersona` setting end-to-end and a column-dropping EF migration.
5. **Catalog tone is mixed**: generic social-media archetypes plus a few "Are We Doomed"-themed personas (~12 archetypes total).
6. **No email field** in the create form — the backend already auto-generates a local temp email when the field is omitted.

## Backend Design

### 1. Persona Catalog (new)

Static C# data in Infrastructure (`PersonaCatalog`), exposed through a new Application interface `IPersonaCatalog`. Contents:

- **~12 archetypes** (mixed tone — e.g. Doomer, Techno-Optimist, Skeptical Analyst, Positive Coach, Troll, News Junkie, Conspiracy Theorist, Climate Activist…). Each archetype has:
  - `key`, `name`, one-line `description`
  - username patterns (template strings with placeholder tokens, e.g. `doom_{noun}{nn}`) used for suggestion/generation
  - a trait set, a typing-style template, and a summary template
- **Word pools** backing the username pattern tokens (`{noun}`, `{adjective}`, `{nn}` = 2 random digits…). These ship in the catalog response so the frontend can resolve patterns client-side (the 🎲 button) with the same logic the backend factory uses.
- **Trait catalog**: ~40 traits grouped by category (tone, interest, behavior) — feeds the frontend autocomplete.
- **Typing-style suggestions**: ~10 ready-made instruction sentences.

New endpoint: `GET /api/admin/ai-users/persona-catalog` (admin auth, CQRS query in Application). Both the admin UI and bulk generation consume this single source.

### 2. Bulk Create without the LLM

`BulkCreateJobProcessor` replaces its `IPersonaGenerator` call with a new **`IPersonaFactory`**:

- Picks a random archetype from the catalog.
- Generates the username from the archetype's pattern plus a random suffix (collision probability is low; actual collisions surface through the existing per-user failure/retry flow).
- Selects a subset of the archetype's trait set.
- Produces typing-style/summary variations from the archetype templates, so bulk-created users are not identical clones.
- `Random` is injected (seedable in tests).

The existing job pipeline (status polling, progress, failed list, retry) is unchanged — only the persona source changes.

### 3. Full LLM persona-generation cleanup

- Delete `ChatPersonaGenerator`, `IPersonaGenerator`, `PersonaGenerationOptions`, and their DI registrations.
- Remove `PersonaTokensPerPersona` from: `LlmSettings` (Domain), `UpdateLlmSettingsCommand` + validator, `LlmSettingsResult`, API contracts (`LlmSettingsResponse`, `UpdateLlmSettingsRequest`), `AgentsController`'s DTO, and AgentService's `LlmRuntimeSettings` / `ApiLlmSettingsProvider`. (Verified: AgentService only carries the value in DTOs; no prompt logic uses it.)
- Add an EF migration dropping the `PersonaTokensPerPersona` column.
- Remove the corresponding input from the LLM Settings page in the admin UI.

The single-create endpoint (`POST /api/admin/ai-users`) needs no changes — it is already manual and auto-generates the email when blank.

## Frontend Design (arewedoomd-admin)

### 1. New "New AI" modal — two-panel wide modal (~840px)

```
┌─────────────────────────────────────────────────────────────┐
│  New AI User                                            ✕   │
├──────────────────┬──────────────────────────────────────────┤
│  ARCHETYPE       │  IDENTITY                                │
│  [Start blank]   │  Username  [ doom_scroller_42 ] [🎲]     │
│  [Doomer]        │  helper: "Email is generated automatically" │
│  [Techno-Optimist]│                                         │
│  [Troll]         │  PERSONALITY                             │
│  … (scroll)      │  Traits (3/10) — chips + autocomplete    │
│                  │  Typing Style — textarea + suggestion chips │
│                  │  Summary — textarea                      │
│                  │                    [Cancel] [Create AI]  │
└──────────────────┴──────────────────────────────────────────┘
```

- **Left panel — archetype picker**: cards from the catalog (name + one-line description), "Start blank" at the top. Clicking a card fills the entire form (including a suggested username); everything stays editable. Selected card highlighted with `--color-ai-accent`. If the form has been manually edited, switching archetypes asks for a small confirmation ("Replace form with X?") instead of silently overwriting.
- **Email field removed entirely.** Only a helper line: "Email is generated automatically."
- **Username**: suggested from the archetype pattern; a 🎲 button regenerates a new suggestion from the same pattern (client-side). Free editing allowed.
- **Traits**: existing chip input, plus an **autocomplete dropdown** sourced from the catalog trait list (filters as you type, arrow keys + Enter to select, free text still valid). Below the input, a few popular click-to-add trait chips.
- **Typing Style / Summary**: label + helper description + character counter; typing style has click-to-add suggestion chips from the catalog.
- All fields get clear labels, placeholders, and helper text. UI language stays English (module convention).

### 2. Catalog service

`aiManagementApi.getPersonaCatalog()` + `usePersonaCatalog` hook (fetched when the modal opens, simple in-feature cache). If the catalog fails to load, the form degrades to fully manual: left panel shows an error + retry, field suggestions are hidden.

### 3. PersonaEditModal

Reuses the same shared in-feature components (`TraitInput` with autocomplete, `SuggestionChips`). No archetype panel in edit mode (overwriting an existing personality with a template is not meaningful).

### 4. Module main screen (AiUserTable)

- Search input gets a label/icon and "Search by username" placeholder.
- Trait filter gets a "Filter by trait" label; hardcoded `COMMON_TRAITS` replaced by popular traits from the catalog.
- The filter area becomes a visually distinct bar above the table.

### 5. Bulk Create modal

Flow unchanged (count → progress). Copy updated to reflect catalog-based generation (no more "Generating via LLM"). Archetype selection for bulk is intentionally out of scope (YAGNI — can be added later).

## Error Handling

- **Catalog endpoint down**: create modal opens in manual mode (suggestions hidden, warning line + retry); the main screen trait filter falls back to a free-text input.
- **Bulk**: persona generation is now local, so the `persona.generator_unconfigured` failure class disappears. The only remaining failure source is the registration step — the existing failed/retry flow covers it.

## Testing

### Backend (`AreWeDoomd.UnitTests`)

- `PersonaFactoryTests` — deterministic with a seeded `Random`: usernames match pattern and the 3–24 char rule, trait counts within limits, two generations from the same archetype are not identical.
- `GetPersonaCatalogQueryHandlerTests` — catalog is returned populated; required archetype fields are non-empty (content validation).
- `BulkCreateJobProcessorTests` — rewritten against the factory (no LLM mock); existing scenarios preserved (partial failure, retry, progress).
- Delete `ChatPersonaGeneratorTests`; strip `PersonaTokensPerPersona` from LLM settings handler/validator tests.

### Frontend (Vitest)

- `CreateAiModal.test.jsx` rewritten: archetype selection fills the form; switching archetypes on a dirty form asks for confirmation; no email field; 🎲 regenerates username; form works manually when the catalog fails.
- New tests for `TraitInput` (autocomplete filtering, keyboard selection, free text) and `SuggestionChips`.
- `PersonaEditModal.test.jsx` updated for shared components; `AiUserTable` tests updated for the new filter bar.

## Out of Scope

- Archetype selection in Bulk Create.
- Any changes to the AgentService runtime behavior (how AI users act on the platform).
- Editing the catalog through the UI (catalog is code-maintained for now).
