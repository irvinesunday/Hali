# Hali — Lessons Learned from Copilot Reviews

This file is append-only. Never delete entries.

After every PR that generates Copilot review comments:
1. Fix the issue in code
2. Add an entry here in the same commit that fixes it
3. Add or update the corresponding rule in CODING_STANDARDS.md

Entry format:

```
## PR #N — short description
**File:** path/to/file.cs
**What Copilot flagged:** exact description
**Root cause:** why it happened
**Fix applied:** what was changed
**Rule added:** which checklist item this maps to
```

---

## PR #69 — OpenAPI v0.5.0 + app_feedback migration

### Lesson 1: Tabs instead of 4-space indentation (CI failure)
**File:** `src/Hali.Api/Controllers/FeedbackController.cs`
**What Copilot flagged:** WHITESPACE errors on lines 17–24, CI exited with code 2
**Root cause:** Claude Code generated the file with tab characters instead of the
4-space indentation expected by the repository's formatting checks. `dotnet format`
detected 8 violations and blocked the CI pipeline.
**Fix applied:** Rewrote the file with correct 4-space indentation throughout.
**Rule added:** Pre-Commit Checklist → Formatting → "run dotnet format --verify-no-changes"

---

### Lesson 2: Advertised 429 with no rate-limiting logic
**File:** `src/Hali.Api/Controllers/FeedbackController.cs`
**What Copilot flagged:** Controller declares [ProducesResponseType(429)] and
docstring says "Rate-limited by IP/session" but the action has zero rate-limiting
logic and can never actually return 429.
**Root cause:** Copied the response code from the OpenAPI spec without verifying
the implementation matched. Spec described the intended future state; code described
the actual present state. They must agree.
**Fix applied:** Removed [ProducesResponseType(429)] and the rate-limiting
docstring comment. Will re-add when rate limiting is actually implemented.
**Rule added:** Pre-Commit Checklist → Response codes and contracts → "every
[ProducesResponseType] must have corresponding logic"

---

### Lesson 3: `object` parameter type disabling model validation
**File:** `src/Hali.Api/Controllers/FeedbackController.cs`
**What Copilot flagged:** `Submit([FromBody] object payload)` prevents ASP.NET
model validation and diverges from the OpenAPI FeedbackRequest schema which
has a required `rating` field and length constraints.
**Root cause:** Wrote a stub controller using `object` as a placeholder. The
placeholder made it past review because the intent was "implement later" but
the contract was already published.
**Fix applied:** Created `SubmitFeedbackRequest` DTO in `Hali.Contracts.Requests`
with [Required] and [MaxLength] annotations matching the spec. Updated controller
to use the typed DTO.
**Rule added:** Pre-Commit Checklist → Contracts and types → "no object/dynamic
as parameter type"

---

### Lesson 4: Spec declares endpoint with no controller implementation
**File:** `docs/arch/02_openapi.yaml`
**What Copilot flagged:** GET /v1/localities/resolve-by-coordinates was declared
in the OpenAPI spec but LocalitiesController had no corresponding action. The
spec claimed the endpoint existed; the backend could not serve it.
**Root cause:** The spec was written ahead of implementation as a planning artifact.
The prompt that updated the spec did not require a simultaneous controller stub.
**Fix applied:** Added [AllowAnonymous] stub action to LocalitiesController
returning 404 until ILocalityService.ResolveByCoordinatesAsync is implemented.
**Rule added:** Pre-Commit Checklist → OpenAPI spec consistency → "every route
in spec must have a matching controller action"

---

## PR #70 — fix(docs): harden COPILOT_RESOLUTION_SKILL

### Lesson 5: Two documents describe the same procedure with different wording
**File:** `CLAUDE.md` lines 506–508
**What Copilot flagged:** CLAUDE.md said "resolve the conversation thread via
GraphQL" but COPILOT_RESOLUTION_SKILL.md only documented the REST reply step
(gh api .../replies) with no GraphQL mutation. The two documents contradicted
each other, which would confuse future sessions about what action was required.
**Root cause:** CLAUDE.md was written as a summary that partially re-documented
a procedure instead of fully deferring to the skill file. When the skill file
was later updated, the CLAUDE.md summary became stale.
**Fix applied:**
  - CLAUDE.md updated to defer to skill file without re-documenting the steps
  - COPILOT_RESOLUTION_SKILL.md updated with the complete GraphQL
    resolveReviewThread mutation and exact CLI commands
**Rule added:** Pre-Commit Checklist → Cross-document consistency → "if a
procedure is described in two places, defer from summary to canonical source"

---

## PR #73 — feat(mobile): Phase A design tokens

### Lesson 6: Peer dependency conflict requiring --legacy-peer-deps
**File:** `apps/citizen-mobile/package.json`
**What Copilot flagged:** (noted in PR, not a blocking review)
`@expo-google-fonts/geist` required `--legacy-peer-deps` because expo-router@55
declares a peer of `@testing-library/react-native@>=13.2.0` but the project
pins `^12.9.0`. Conflict is in devDependencies only, does not affect runtime.
**Root cause:** Testing library version was pinned conservatively and not updated
when expo-router was upgraded.
**Fix applied:** Installed with --legacy-peer-deps. Flagged in PR notes.
**Rule added:** When a font or UI package requires --legacy-peer-deps, always
document why in the PR body. Schedule a follow-up to resolve the underlying
version conflict.

---

## PR #75 — chore(docs): CODING_STANDARDS + LESSONS_LEARNED

### Lesson 7: Formatting enforcement referenced non-existent .editorconfig
**File:** `docs/arch/CODING_STANDARDS.md`, `docs/arch/LESSONS_LEARNED.md`
**What Copilot flagged:** Both files implied 4-space indentation was enforced by
an `.editorconfig`, but this file does not exist in the repo. The rule and the
Lesson 1 root cause were therefore incorrect.
**Root cause:** Assumed .editorconfig as the source of enforcement without verifying
the repo contained one. The actual enforcement mechanism is `dotnet format` defaults.
**Fix applied:** Removed `.editorconfig` references. Reworded checklist items to
describe the actual enforcement. Updated Lesson 1 root cause accordingly.
**Rule added:** Pre-Commit Checklist → Formatting → "C# files: use spaces, not tabs; 4-space indentation"

---

### Lesson 8: Misleading `var` rule targeting something that cannot exist
**File:** `docs/arch/CODING_STANDARDS.md` line 23
**What Copilot flagged:** The rule "no var for public API surface types" is
unactionable because `var` cannot appear in public method signatures or
properties — only in local variables. The rule was too broad and confusing.
**Root cause:** Wrote the rule at a high level of abstraction without verifying
what `var` can actually appear in.
**Fix applied:** Scoped the rule to local variables in controller/contract-related
code where the type is not obvious from the right-hand side.
**Rule added:** Pre-Commit Checklist → Contracts and types → explicit local type rule

---

### Lesson 9: Hardcoded relative path `../../theme` in checklist
**File:** `docs/arch/CODING_STANDARDS.md` lines 48–50
**What Copilot flagged:** `../../theme` hardcodes a relative path depth that
will be incorrect for components at different nesting levels.
**Root cause:** Copied a concrete import path from a specific component without
thinking about the rule's generality.
**Fix applied:** Changed to reference the canonical `src/theme` barrel module,
letting each file use its own appropriate relative import.
**Rule added:** Pre-Commit Checklist → React Native → theme barrel import rule

---

### Lesson 10: Validation rule contradicted existing controller patterns
**File:** `docs/arch/CODING_STANDARDS.md` line 82
**What Copilot flagged:** "Validation via DataAnnotations only, not manual
if-checks" contradicts existing controllers that use manual checks for query
params and cross-field constraints.
**Root cause:** Stated an aspirational standard as an absolute rule without
accounting for the cases where DataAnnotations cannot express the constraint.
**Fix applied:** Narrowed the rule to DTO/body validation. Explicitly permits
manual checks for query parameters and derived/cross-field constraints.
**Rule added:** C# conventions → Controller rules → clarified validation rule

---

### Lesson 11: Null check rule was technically incorrect for nullable reference types
**File:** `docs/arch/CODING_STANDARDS.md` line 93
**What Copilot flagged:** "Never use == null guards" contradicts C# language
semantics and existing repo usage. `??` and `?.` serve different purposes
than explicit null checks.
**Root cause:** Over-prescriptive phrasing that conflated null-coalescing
operators (??/?) with null equality checks (== null / is null).
**Fix applied:** Rewrote as a positive preference: "prefer `is null`/`is not null`
for explicit null checks in new code" without banning `== null`.
**Rule added:** C# conventions → Nullable reference types → updated null check guideline

---

### Lesson 12: Hard rules stated as absolute when legacy codebase contradicts them
**File:** `docs/arch/CODING_STANDARDS.md` lines 127, 48–50
**What Copilot flagged:** Rules like "never hardcode hex colours" are absolute
but the current mobile codebase contains many hardcoded values. As written, the
checklist would always fail on any file that touches existing components.
**Root cause:** Rules were written for greenfield code but applied as
repository-wide absolutes without acknowledging existing legacy code.
**Fix applied:** Scoped the rules to "new or modified code" with an explicit note
that legacy values may be migrated incrementally.
**Rule added:** What never to do → scoped hardcode rule

---

### Lesson 13: Empty heading elements from `#` comment-style lines in Markdown
**File:** `docs/arch/LESSONS_LEARNED.md` lines 1–15
**What Copilot flagged:** Standalone `#` lines (e.g. `# This file is append-only`)
render as empty `<h1>` elements in Markdown, producing odd TOC entries and
unexpected formatting.
**Root cause:** Used shell/Python comment syntax (`#`) to write prose instructions
in a Markdown file, not realising `#` is a heading marker in Markdown.
**Fix applied:** Converted the header block to regular prose paragraphs and a
fenced code block for the entry format template.
**Rule added:** When writing instruction prose in Markdown files, use regular
paragraphs — not `#`-prefixed lines.

---

### Lesson 14: Inconsistent field names across entries and template
**File:** `docs/arch/LESSONS_LEARNED.md` line 15
**What Copilot flagged:** The format block at the top used `Rule added to
CODING_STANDARDS.md:` but existing lesson entries used the shorter `Rule added:`.
Inconsistency makes future entries harder to search.
**Root cause:** Wrote the template with a long descriptive field name but used a
shorter one when writing the actual entries.
**Fix applied:** Standardised all entries and the template to `**Rule added:**`.
**Rule added:** LESSONS_LEARNED.md template field names must match existing entries.

---

### Lesson 15: Minimum command lists not rendering as code blocks in CLAUDE.md
**File:** `CLAUDE.md` lines 547–553
**What Copilot flagged:** The "Minimum commands" sections used 2-space indentation,
which does not reliably render as a code block in all Markdown renderers.
**Root cause:** Used prose indentation rather than a fenced code block for
command sequences.
**Fix applied:** Wrapped each command sequence in a fenced code block.
**Rule added:** When listing shell commands in Markdown, always use fenced code
blocks, not indented prose.

---

## PR #29 — docs(arch): institution dashboard canonical spec v1.1

### Lesson 16: Spec referenced non-existent external documents
**File:** `docs/arch/hali_institution_dashboard_canonical_spec.md`
**What Copilot flagged:** The spec cited `Hali_05_Repository_Engineering_Conventions_Claude_Seed.docx` and `Hali_03_API_Contracts_Client_Interaction_Spec.docx` which do not exist in the repository. Reviewers/implementers have no way to access those references.
**Root cause:** Spec was ported from an external authoring context (Word docs) without replacing external citations with in-repo authority links.
**Fix applied:** Replaced doc references with pointers to in-repo authorities (`CLAUDE.md`, `mvp_locked_decisions.md`, `02_openapi.yaml`).
**Rule added:** Pre-Commit Checklist → Cross-document consistency → "never reference an external or non-repo document; always link to an in-repo authority"

---

### Lesson 17: Spec introduced endpoints not declared in OpenAPI
**File:** `docs/arch/hali_institution_dashboard_canonical_spec.md`
**What Copilot flagged:** The spec's API dependency table listed `GET /v1/institutions/dashboard/summary` and `GET /v1/institutions/dashboard/clusters`, plus `POST /v1/institutions/updates`, none of which existed in `02_openapi.yaml`. Implementers would build against undeclared routes.
**Root cause:** The spec was written as a design aspirational document without being reconciled against the existing OpenAPI contract. New routes were invented inline without marking them as "proposed".
**Fix applied:** Flagged proposed endpoints explicitly in the spec as requiring an OpenAPI contract update before implementation.
**Rule added:** Pre-Commit Checklist → OpenAPI spec consistency → "any endpoint named in a spec or arch doc that is not in 02_openapi.yaml must be marked 'proposed' with a note that it requires an OpenAPI + backend change"

---

### Lesson 18: Idempotency mechanism inconsistency between spec and implementation
**File:** `docs/arch/hali_institution_dashboard_canonical_spec.md`
**What Copilot flagged:** The institution spec required an `Idempotency-Key` request header, but the existing API contract uses `idempotencyKey` in the JSON body. The two mechanisms are incompatible; a client would implement the wrong one.
**Root cause:** The spec author used header-based idempotency (common in some APIs) without checking the existing API contract's established pattern.
**Fix applied:** Updated spec to use `idempotencyKey` in the request body, consistent with all other mutation endpoints.
**Rule added:** Pre-Commit Checklist → Contracts and types → "idempotency key goes in the request body as `idempotencyKey` — never in an `Idempotency-Key` header"

---

### Lesson 19: Spec referenced a non-existent schema table (`institution_memberships`)
**File:** `docs/arch/hali_institution_dashboard_canonical_spec.md`
**What Copilot flagged:** The spec's auth scoping section described `institution_memberships` as controlling access, but no such table exists; institution scope is encoded directly in `accounts.institution_id`.
**Root cause:** The spec described a planned multi-membership model without noting that it was not yet implemented and would require a schema migration.
**Fix applied:** Updated auth scoping section to match the actual data model, with explicit "planned schema change" callout.
**Rule added:** Pre-Commit Checklist → Migrations → "if a spec references a DB table/column that does not exist in the current schema, mark it explicitly as a planned schema change"

---

## PR #31 — docs(arch): add citizen mobile app canonical spec v1.1

### Lesson 20: Spec version numbers hardcoded in example UI copy
**File:** `docs/arch/hali_citizen_mvp_canonical_spec.md`
**What Copilot flagged:** The Profile screen footer example showed "Hali v1.0" while the document itself was v1.1. Hardcoded version strings in example UI copy cause doc/product drift every time the version increments.
**Root cause:** Wrote a concrete example string with a literal version number without considering that the doc version and app version evolve independently.
**Fix applied:** Removed literal version numbers from UI copy examples; noted that the app version is rendered dynamically from build metadata.
**Rule added:** When writing UI copy examples in spec documents, do not hardcode version numbers — use a placeholder like `[app_version]` instead.

---

### Lesson 21: Spec allowed states not tracked in the actual `SignalState` enum
**File:** `docs/arch/hali_citizen_mvp_canonical_spec.md`
**What Copilot flagged:** The spec listed only four cluster states (`unconfirmed | active | possible_restoration | resolved`), but the backend's `SignalState` enum also includes `expired` and `suppressed`. Clients built from the spec alone would not handle these states.
**Root cause:** The spec was written from product documentation that predated the backend implementation, which added additional operational states.
**Fix applied:** Added the full enum surface to the spec with notes on how `expired` and `suppressed` should be rendered in the citizen app UI.
**Rule added:** Pre-Commit Checklist → Cross-document consistency → "enum values in UI specs must match the full set of values in the backend enum, or explicitly document which subset is exposed"

---

## PR #32 — docs: consolidated architecture pack

### Lesson 22: Participation index predicate used wrong column for deduplication
**File:** `docs/arch/01_schema_reference.md`, `docs/arch/03_phase1_backend.md`, `Hali_Platform_Reconciliation_v1.md`, `docs/arch/00_session_patch_notes.md`
**What Copilot flagged:** The partial unique index `ix_participations_device_cluster_type` was predicated on `WHERE idempotency_key IS NOT NULL`, but idempotency keys are optional — so the index does not prevent duplicate participations when `idempotency_key` is NULL. The correct predicate to enforce "one participation type per device per cluster" is `WHERE device_id IS NOT NULL`.
**Root cause:** Index was designed to serve double-duty (both replay protection and deduplication). Using `idempotency_key` as the predicate only covers the idempotency case. The deduplication concern requires `device_id`.
**Fix applied:** Updated the index predicate across all documentation to `WHERE device_id IS NOT NULL`. Noted that idempotency replay protection uses a separate index on `idempotency_key`.
**Rule added:** When designing partial unique indexes for deduplication, the predicate must guard the uniqueness columns (`device_id`, `cluster_id`, `participation_type`) — not the idempotency field.

---

### Lesson 23: CIVIS pseudocode referenced undefined local variables
**File:** `docs/arch/05_civis_engine.md`
**What Copilot flagged:** In `ComputeTemporalDecay`, the variable `category` was used but not defined in scope. In `ComputeSds`, `config.GetEvaluationHorizonMinutes(category)` also referenced `category` which was not a parameter. The pseudocode was not implementable as written.
**Root cause:** Pseudocode was drafted at a high level without verifying that every referenced identifier was in scope or passed as a parameter.
**Fix applied:** Added `category` as an explicit parameter to both methods in the spec pseudocode; updated callers.
**Rule added:** CIVIS pseudocode in `docs/arch/05_civis_engine.md` must compile logically — every referenced variable must be defined in scope or passed as a parameter.

---

### Lesson 24: Queue event name in worker pipeline did not match the queue key table
**File:** `docs/arch/06_worker_pipelines.md`
**What Copilot flagged:** The clustering step emitted `signal.nlp_enriched → queue:cluster-linking`, but neither the event type nor the queue name appeared in the document's own "Queue key mapping" table. This leaves routing ambiguous for implementers.
**Root cause:** The step was written in isolation without cross-checking the queue key table defined earlier in the same document.
**Fix applied:** Added `signal.nlp_enriched` event type and `queue:cluster-linking` queue to the mapping table.
**Rule added:** Pre-Commit Checklist → Cross-document consistency → "every queue name and event type referenced in a worker pipeline doc must appear in the queue key mapping table of the same document"

---

### Lesson 25: NLP confidence clamp ceiling inconsistent with confidence cap policy
**File:** `docs/arch/09_nlp_integration.md`
**What Copilot flagged:** `Math.Clamp(..., 0.0m, 1.0m)` clamped to 1.0, but a separate policy rule in the same doc stated CSI-NLP must never return confidence > 0.95. The two values were inconsistent — either the clamp ceiling or the policy cap needed to be updated.
**Root cause:** The clamp was written as a default range without checking the domain-specific max confidence rule documented a few lines later.
**Fix applied:** Updated clamp to `Math.Clamp(..., 0.0m, 0.95m)` to be consistent with the max confidence policy.
**Rule added:** Pre-Commit Checklist → Contracts and types → "NLP confidence values must be clamped to [0.0, 0.95], not [0.0, 1.0]"

---

### Lesson 26: Enum value in API contract example did not match schema enum definition
**File:** `docs/arch/09_nlp_integration.md`, `docs/arch/02_api_contracts.md`
**What Copilot flagged:** Both the NLP output example and the API contract preview candidate used `locationPrecisionType: "road_landmark"`, but the schema enum for `location_precision_type` is `area|road|junction|landmark|facility|pin`. The example value was invalid.
**Root cause:** The example value was written from memory of the intended types without checking the canonical enum in `01_schema_reference.md`.
**Fix applied:** Updated both example values to `"landmark"`, which is a valid enum value.
**Rule added:** Pre-Commit Checklist → OpenAPI spec consistency → "example/sample values in docs and API contracts must use only values from the canonical schema enum, not invented synonyms"

---

### Lesson 27: Duplicate instruction entrypoint — `claude.md` and `CLAUDE.md` coexisting
**File:** `CONSOLIDATION_GUIDE.md`, `claude.md`, `CLAUDE.md`
**What Copilot flagged:** The repo contained both `claude.md` (lowercase, legacy) and `CLAUDE.md` (uppercase, canonical). The consolidation guide instructed removing the old file on case-sensitive filesystems, but it remained. On Linux (the CI/CD host) these are two separate files, creating two instruction entrypoints with potentially conflicting content.
**Root cause:** The consolidation script was not run or the deletion step was skipped. Both files accumulated separate changes over subsequent PRs.
**Fix applied:** Removed `claude.md`; designated `CLAUDE.md` as the sole instruction entrypoint.
**Rule added:** Pre-Commit Checklist → Cross-document consistency → "there must be exactly one `CLAUDE.md` at the repo root; `claude.md` must not exist"

---

### Lesson 28: Coordinate formatting in NLP prompt not culture-invariant
**File:** `docs/arch/09_nlp_integration.md`
**What Copilot flagged:** Coordinate values were formatted with `UserLatitude?.ToString()`, which is culture-sensitive and may emit commas as decimal separators on locales using European number formats (e.g., `1,234` instead of `1.234`). This can break NLP prompt parsing.
**Root cause:** Used the default `ToString()` overload for numeric types without specifying `CultureInfo.InvariantCulture`.
**Fix applied:** Updated coordinate formatting to use `ToString("F6", CultureInfo.InvariantCulture)`.
**Rule added:** C# conventions → "always use `CultureInfo.InvariantCulture` when converting numeric types to strings for external system consumption (API payloads, NLP prompts, log messages)"

---

## PR #34 — Chore/pr template claude md update

### Lesson 29: CI job count hardcoded in a checklist
**File:** `claude.md` (legacy — since replaced by `CLAUDE.md`)
**What Copilot flagged:** The PR checklist item "All 6 CI jobs are green" was outdated — the actual CI pipeline had grown to more than 6 jobs. The hardcoded count would cause contributors to miss failures in newer jobs.
**Root cause:** Wrote the checklist with a concrete job count at a point in time without accounting for future pipeline additions.
**Fix applied:** Changed to "All required CI jobs are green" — no hardcoded count.
**Rule added:** Pre-Commit Checklist → never hardcode a count of CI jobs in checklists or documentation; use "all required" instead.

---

### Lesson 30: Branching strategy rule internally contradicted itself
**File:** `claude.md` (legacy — since replaced by `CLAUDE.md`)
**What Copilot flagged:** The branching section stated that `main` is updated via a release PR from `develop`, then immediately said "never use `--base main`". This contradiction would cause implementers to break the release process.
**Root cause:** An absolute rule ("never use `--base main`") was written without carving out the legitimate exception (release PRs).
**Fix applied:** Qualified the rule: "never use `--base main` for feature/fix/chore PRs; only release PRs from `develop` may target `main`".
**Rule added:** When writing absolute rules in documentation, always check whether the rule has legitimate exceptions and document them.

---

## PR #38 — Merge develop to main

### Lesson 31: Backend runtime version stated as .NET 9 when codebase targets .NET 10
**File:** `CLAUDE.md`, `.github/instructions/hali.instructions.md`
**What Copilot flagged:** Multiple locations in `CLAUDE.md` described the backend as ".NET 9", but all project files target `net10.0` and CI uses `DOTNET_VERSION=10.0.x`. Contributors reading the docs would install the wrong SDK.
**Root cause:** The docs were drafted before the final SDK version was confirmed, and were not updated when the target framework was changed to net10.0.
**Fix applied:** Updated all ".NET 9" references to ".NET 10" in CLAUDE.md and the GitHub Copilot instructions file.
**Rule added:** Pre-Commit Checklist → Cross-document consistency → "verify that all .NET version references in docs match the `TargetFramework` in `*.csproj` files before committing"

---

### Lesson 32: GitHub Copilot instruction file contradicted arch doc on refresh token storage
**File:** `.github/instructions/hali.instructions.md`
**What Copilot flagged:** The Copilot instructions file said reviewers should not suggest storing refresh tokens in cookies, but `docs/arch/07_auth_implementation.md` explicitly requires httpOnly cookies for web surfaces. This would cause Copilot to incorrectly dismiss correct suggestions for Phase 2 web auth.
**Root cause:** The Copilot instructions file was written for Phase 1 (mobile-only, no cookies) without adding a Phase 2 carve-out.
**Fix applied:** Updated instructions to allow httpOnly cookies for web surfaces per the arch doc, while still prohibiting localStorage.
**Rule added:** Pre-Commit Checklist → Cross-document consistency → "Copilot/GitHub instruction files must not contradict `docs/arch/` files on security-critical topics"

---

### Lesson 33: Offline queue helper omitted idempotency key on the online fast path
**File:** `docs/arch/04_phase1_mobile.md`
**What Copilot flagged:** The `enqueueOrSubmit` helper only attached `idempotencyKey` to queued (offline) items during flush, but submitted the raw body without a key on the online path. Since the API contract requires `idempotencyKey` on all mutations, a timed-out request could not be safely retried.
**Root cause:** The idempotency key was generated only at flush time, treating it as an offline-queue-only concern rather than a property of every mutation request.
**Fix applied:** Updated the helper to generate `idempotencyKey` upfront and include it in both the online submit and the queued item so retries reuse the same key.
**Rule added:** Pre-Commit Checklist → Contracts and types → "every mutation request must include `idempotencyKey` on both the online and offline code paths"

---

### Lesson 34: Signal composer character limit inconsistent between mobile spec and citizen MVP spec
**File:** `docs/arch/04_phase1_mobile.md`
**What Copilot flagged:** The mobile spec allowed 500 characters for the report composer input, but the citizen MVP spec (`hali_citizen_mvp_canonical_spec.md` §10.3) locked it to 150 characters. Both documents would drive different UI implementations.
**Root cause:** Two spec documents were updated independently without cross-checking character limits.
**Fix applied:** Aligned `04_phase1_mobile.md` to the locked 150-character limit.
**Rule added:** Pre-Commit Checklist → Cross-document consistency → "composer/input character limits must be consistent between `04_phase1_mobile.md` and `hali_citizen_mvp_canonical_spec.md`"

---

## PR #39 — chore: add self-healing CI skill and mandatory iteration rule

### Lesson 35: SELF_HEALING_SKILL.md documented an unsupported `dotnet ef` flag
**File:** `docs/arch/SELF_HEALING_SKILL.md`
**What Copilot flagged:** The fix table instructed adding `--idempotent` to `dotnet ef database update`, but that flag is not supported by `database update` — it is a flag for `dotnet ef migrations script`. Following this guidance would produce a CLI error.
**Root cause:** The `--idempotent` flag from `migrations script` was incorrectly applied to the `database update` command from memory without verifying CLI documentation.
**Fix applied:** Corrected the fix table to use `dotnet ef migrations script --idempotent` for generating an idempotent SQL script, or describe the correct approach for repairing `__EFMigrationsHistory`.
**Rule added:** Before documenting any CLI command in architecture or skill files, verify the command and all flags against official documentation or `--help` output.

---

### Lesson 36: CI step order example ran `dotnet ef` without `--no-build` after an explicit build step
**File:** `docs/arch/SELF_HEALING_SKILL.md`
**What Copilot flagged:** The "Step order for .NET CI jobs" example ran an explicit `dotnet build` and then `dotnet ef database update` without `--no-build`, triggering a second build. The text immediately below the example stated "Never use `--no-build` unless the build step ran in the same job earlier" — which contradicted the example, since the build *had* just run.
**Root cause:** The example was written without checking whether it matched the accompanying rule. The rule and example described opposite behavior.
**Fix applied:** Updated the example to add `--no-build` to the EF call when a prior build step is present in the same job.
**Rule added:** C# conventions → "when a `dotnet build` step precedes a `dotnet ef` step in the same CI job, always pass `--no-build` to the EF command to avoid redundant builds"

---

### Lesson 37: Fix table hardcoded a branch name for merge conflict resolution
**File:** `docs/arch/SELF_HEALING_SKILL.md`
**What Copilot flagged:** The fix-table row for "Merge conflict" instructed `git merge origin/develop`, but PRs can target branches other than `develop` (e.g., release PRs target `main`). Merging from `develop` on a non-develop-targeting branch would introduce wrong history.
**Root cause:** The instruction was written assuming all feature branches target `develop`, without acknowledging the release PR flow.
**Fix applied:** Updated the fix table to reference "the PR's actual base branch" rather than hardcoding `origin/develop`.
**Rule added:** When writing git remediation steps in documentation, always refer to the PR's base branch dynamically rather than hardcoding `develop` or `main`.

---

## PR #55 — fix(localities): display names in ward picker and home feed

### Lesson 38: Using `System.Web.HttpUtility.UrlEncode` on .NET 10 target
**File:** `src/Hali.Infrastructure/Signals/NominatimGeocodingService.cs`
**What Copilot flagged:** The service used `System.Web.HttpUtility.UrlEncode`, which is not available on net10.0 without adding the `System.Web.HttpUtility` NuGet package. The build would fail.
**Root cause:** `HttpUtility.UrlEncode` is a common URL-encoding utility from .NET Framework days. On modern .NET, it requires an explicit package reference that was not added.
**Fix applied:** Replaced with `Uri.EscapeDataString(...)`, which is available on all .NET targets without additional packages.
**Rule added:** Pre-Commit Checklist → C# conventions → "never use `System.Web.HttpUtility` — use `Uri.EscapeDataString` or `System.Net.WebUtility` for URL encoding on net10.0"

---

### Lesson 39: `HttpResponseMessage` not disposed after use
**File:** `src/Hali.Infrastructure/Signals/NominatimGeocodingService.cs`
**What Copilot flagged:** The `HttpResponseMessage` returned from `_http.SendAsync` was never disposed, which keeps the underlying connection/socket open longer than necessary and can cause resource exhaustion under load.
**Root cause:** The response object was treated as a simple data holder rather than a disposable resource that wraps a network stream.
**Fix applied:** Wrapped the response in `using var response = await _http.SendAsync(...)` to guarantee disposal after reading content.
**Rule added:** Pre-Commit Checklist → C# conventions → "always wrap `HttpResponseMessage` in a `using` statement to ensure the response and its content stream are disposed"

---

### Lesson 40: GroupBy deduplication could silently discard a non-null `DisplayLabel`
**File:** `src/Hali.Application/Notifications/FollowService.cs`
**What Copilot flagged:** `GroupBy(...).Select(g => g.First())` for deduping follows could pick an entry with a null `DisplayLabel` over a later entry with a valid one, depending on input order. The result would be a follow row that silently lost its label.
**Root cause:** `First()` was used for deduplication without considering that different duplicates may have different non-null field values.
**Fix applied:** Changed to `g.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.DisplayLabel)) ?? g.First()` so the non-null label is preserved.
**Rule added:** Pre-Commit Checklist → C# conventions → "when deduplicating with GroupBy, use a predicate that preserves the most data (non-null fields) rather than always picking `First()`"

---

### Lesson 41: Input length not validated before persisting to a length-constrained column
**File:** `src/Hali.Api/Controllers/LocalitiesController.cs`
**What Copilot flagged:** `dto.Items[i].DisplayLabel` was passed through to the DB without validating that it was within the column's `HasMaxLength(160)` constraint. Oversized input would cause a `DbUpdateException` (500 error) rather than a clean 400/422.
**Root cause:** The DTO and controller were written without matching the `[MaxLength]` annotation to the EF Core fluent configuration. The API boundary did not enforce what the DB layer required.
**Fix applied:** Added `[MaxLength(160)]` to `DisplayLabel` in the DTO so ASP.NET model validation rejects oversized values with 400 before they reach the DB.
**Rule added:** Pre-Commit Checklist → Contracts and types → "every DTO field that maps to a `HasMaxLength(N)` EF column must have a matching `[MaxLength(N)]` DataAnnotation"

---

## PR #56 — fix(auth): register PostgreSQL custom enums with Npgsql

### Lesson 42: NpgsqlDataSource not registered in DI — won't be disposed on shutdown
**File:** `src/Hali.Infrastructure/Extensions/ServiceCollectionExtensions.cs`
**What Copilot flagged:** `NpgsqlDataSource` instances were constructed inside `AddInfrastructure` and captured in lambdas, but not registered in the DI container. The host would never call `Dispose` on them, leaving the connection pool alive after shutdown.
**Root cause:** The data source was wired into `UseNpgsql()` as a captured closure rather than a DI singleton, bypassing the container's lifetime management.
**Fix applied:** Registered each `NpgsqlDataSource` as a singleton in the DI container so the `ServiceProvider` disposes it on shutdown.
**Rule added:** Pre-Commit Checklist → C# conventions → "any `IDisposable`/`IAsyncDisposable` infrastructure object (NpgsqlDataSource, HttpClient, etc.) must be registered in the DI container to ensure proper disposal"

---

### Lesson 43: PR scope not reflected in PR description
**File:** `02_openapi.yaml` (PR-level comment)
**What Copilot flagged:** The PR description focused only on the Npgsql enum mapping fix, but the PR also introduced a new `/v1/localities/search` endpoint, changed the `followed` response shape, added `display_label` DB persistence, and added a new migration. Reviewers had no way to assess the full scope.
**Root cause:** The PR was originally scoped as a small fix and grew during implementation without the description being updated.
**Fix applied:** Updated the PR description to enumerate all changed areas (enum fix, new endpoint, response shape change, migration).
**Rule added:** Pre-Commit Checklist → "update the PR description to enumerate all changed areas before requesting review — the description must match the actual diff scope"

---

## PR #57 — fix: resolve unresolved Copilot comments from PR #55

### Lesson 44: Unit test covered null but not empty string / whitespace for same guard
**File:** `tests/Hali.Tests.Unit/Notifications/FollowServiceTests.cs`
**What Copilot flagged:** The new dedupe logic used `IsNullOrWhiteSpace` to check `DisplayLabel`, but the unit test only covered the null→non-null case. Empty string and whitespace inputs were not tested, so a regression restoring the old behaviour for those cases would go undetected.
**Root cause:** Test was written to cover the failing case (null) that was reported, without extending to the other values the guard handles.
**Fix applied:** Added test cases for `""` and `" "` (whitespace-only) DisplayLabel values to fully cover the `IsNullOrWhiteSpace` branch.
**Rule added:** Pre-Commit Checklist → TEST → "when a guard uses `IsNullOrWhiteSpace`, the test suite must cover null, empty string, and whitespace inputs independently"

---

## PR #60 — fix(security): resolve 23 CodeQL findings (log injection, PII, workflow perms)

### Lesson 45: Workflow permissions over-granted at the workflow level instead of job level
**File:** `.github/workflows/deploy.yml`, `.github/workflows/ci.yml`
**What Copilot flagged:** Both workflows granted `packages: write`, `id-token: write`, `pull-requests: write`, and `checks: write` at the top-level `permissions:` block, applying them to all jobs. Most jobs did not need these elevated permissions. Least-privilege requires scoping permissions to the specific jobs that need them.
**Root cause:** Permissions were set at the workflow level for convenience during initial CI setup, without reviewing which permissions each job actually required.
**Fix applied:** Moved elevated permissions to job-level `permissions:` blocks, granting only what each job needs. Removed `id-token: write` from workflows with no OIDC usage.
**Rule added:** Pre-Commit Checklist → Security → "GitHub Actions permissions must be set at the job level, not the workflow level; grant only the minimum permission required for each job"

---

### Lesson 46: `issues: write` required to post PR comments via `github.rest.issues.createComment`
**File:** `.github/workflows/ci.yml`
**What Copilot flagged:** The `coverage-gate` job used `github.rest.issues.createComment(...)` to post coverage reports on PRs. This endpoint uses the Issues API and requires `issues: write`, but the workflow only granted `pull-requests: write`. The comment would fail silently with 403.
**Root cause:** The Issues API and Pull Requests API share comment endpoints in GitHub's REST API, which is counterintuitive. `pull-requests: write` was assumed sufficient for PR comment posting.
**Fix applied:** Granted `issues: write` to the `coverage-gate` job and removed the incorrect `pull-requests: write` from that job.
**Rule added:** Pre-Commit Checklist → Security → "posting PR comments via `github.rest.issues.createComment` requires `issues: write`, not `pull-requests: write`"

---

### Lesson 47: Correlation ID echoed in response header without validation — HTTP header injection risk
**File:** `src/Hali.Api/Middleware/CorrelationIdMiddleware.cs`
**What Copilot flagged:** The middleware took the raw `X-Correlation-Id` header value and echoed it directly into the response header and `HttpContext.Items`. If the header contained control characters (CR/LF), this could cause header injection, runtime exceptions when setting the response header, and log-forging via the items dictionary.
**Root cause:** The middleware was written to propagate the client correlation ID for observability without sanitizing the untrusted header value first.
**Fix applied:** Added a `SanitizeCorrelationId` method that allows only `[A-Za-z0-9\-_]{1,64}` and replaces any non-conforming value with a new server-generated GUID.
**Rule added:** Pre-Commit Checklist → Security → "any client-supplied header value that is echoed in a response header or written to logs must be validated/sanitized against an allowlist before use"

---

### Lesson 48: Stale code comment described behavior that was removed in the same commit
**File:** `src/Hali.Api/Middleware/CorrelationIdMiddleware.cs`
**What Copilot flagged:** A comment in `CorrelationIdMiddleware` stated that "method and path are rebuilt from an allowlist before they touch the logger", but the implementation had been changed to omit method/path entirely. The comment described non-existent behavior.
**Root cause:** The comment was written for an earlier version of the middleware and not updated when the implementation was simplified.
**Fix applied:** Removed the stale comment describing method/path sanitization.
**Rule added:** Pre-Commit Checklist → "remove or update comments that describe removed or changed behavior in the same commit as the code change"

---

## PR #61 — chore: add dotnet format lint workflow

### Lesson 49: `dotnet format` tool not installed in CI workflow before use
**File:** `.github/workflows/lint.yml`
**What Copilot flagged:** The lint workflow ran `dotnet format --verify-no-changes` without first installing the `dotnet-format` global tool. GitHub-hosted runners do not include `dotnet-format` by default, so the job would fail with "No executable found matching command 'dotnet-format'".
**Root cause:** The workflow was written assuming `dotnet format` is available as a built-in `dotnet` subcommand without checking whether GitHub runners ship with it.
**Fix applied:** Added a tool installation step before the format check; alternatively, used a tool manifest to restore it.
**Rule added:** Pre-Commit Checklist → "any CI workflow using a .NET global tool must include an explicit `dotnet tool install` or `dotnet tool restore` step before invoking the tool"

---

### Lesson 50: CI workflow hardcoded .NET SDK version instead of using a shared env variable
**File:** `.github/workflows/lint.yml`
**What Copilot flagged:** The lint workflow hardcoded `10.0.x` for the .NET SDK version instead of referencing the shared `env.DOTNET_VERSION` variable used by `ci.yml` and `deploy.yml`. A future SDK version bump would require updating this file separately.
**Root cause:** The workflow was written without checking how other workflows in the repo managed the SDK version.
**Fix applied:** Defined `env: DOTNET_VERSION: "10.0.x"` in the workflow and updated the `actions/setup-dotnet` step to reference `${{ env.DOTNET_VERSION }}`.
**Rule added:** Pre-Commit Checklist → "all GitHub Actions workflows must reference the .NET SDK version via a shared env variable, not a hardcoded string"

---

## PR #71 — docs: use full path for COPILOT_RESOLUTION_SKILL.md reference in DOC_AUDIT_REPORT

### Lesson 51: Shell script in Markdown used escaped quotes inside fenced code block
**File:** `docs/arch/DOC_AUDIT_REPORT.md`
**What Copilot flagged:** The bash snippet in the doc contained unnecessarily escaped quotes (`basename \"$f\"`, `grep -q \"$n\"`). Inside a Markdown fenced code block, the quotes should be literal, not escaped. The escaped form is harder to copy/paste and harder to read.
**Root cause:** Shell command was escaped as if it were being embedded in a JSON string or HTML attribute, not in a Markdown code block.
**Fix applied:** Removed the backslash escaping from all quote characters in the fenced code block.
**Rule added:** In Markdown fenced code blocks, use literal (unescaped) shell syntax. Only escape characters required by shell quoting rules, not by Markdown formatting.

---

### Lesson 52: File references in documentation used inconsistent path depth
**File:** `docs/arch/DOC_AUDIT_REPORT.md`, `CLAUDE.md`
**What Copilot flagged:** Several references to `COPILOT_RESOLUTION_SKILL.md` used only the filename without the `docs/arch/` prefix, while other references in the same document used the full path. Inconsistency makes automated audits and `grep`-based checks unreliable.
**Root cause:** Some references were added quickly with just the filename, while others used the full path. No rule enforced consistency.
**Fix applied:** Standardised all references to use the full path `docs/arch/COPILOT_RESOLUTION_SKILL.md`.
**Rule added:** Pre-Commit Checklist → Cross-document consistency → "file references in Markdown docs must always use the full repo-relative path, not just the filename"

---

### Lesson 53: Parenthetical note hardcoded a PR number that would become stale after merge
**File:** `CLAUDE.md`
**What Copilot flagged:** A note in CLAUDE.md said "(wired separately in PR #70)", which would become stale and confusing once that PR merged. PR number references in documentation become orphaned references with no value after the PR closes.
**Root cause:** Added a contextual note during PR authoring without considering that the note was tied to a transient state (an in-flight PR).
**Fix applied:** Removed the PR number reference and replaced with a timeless reference to the doc path.
**Rule added:** Never include PR numbers as inline references in permanent documentation files. They are valid in commit messages and changelogs, but not in arch docs or CLAUDE.md.

---

## PR #72 — test(integration): BLOCKING-1 — integration test suite

### Lesson 54: Duplicate `public partial class Program` declaration across two files
**File:** `src/Hali.Api/Program.cs`
**What Copilot flagged:** `public partial class Program {}` was added to `Program.cs` to expose the type for `WebApplicationFactory<Program>` in integration tests, but the same declaration already existed in `src/Hali.Api/ProgramEntry.cs`. Two partial class declarations in different files for test-only exposure is confusing and raises ambiguity about which file is the canonical test entry point.
**Root cause:** The integration test scaffolding added the partial class declaration to `Program.cs` without checking for an existing declaration elsewhere in the project.
**Fix applied:** Removed the duplicate declaration from `Program.cs` and consolidated test-exposure in `ProgramEntry.cs`.
**Rule added:** Before adding `public partial class Program` to `Program.cs`, search the project for existing declarations of the same class to avoid duplicates.

---

## PR #77 — chore(docs): wire lesson recording to every PR close point

### Lesson 56: jq `// empty` guard needed when extracting PR number from gh pr list
**File:** `docs/arch/COPILOT_RESOLUTION_SKILL.md`
**What Copilot flagged:** `gh pr list ... --jq '.[0].number'` outputs the literal string `null` when no matching PR is found. Because `null` is non-empty, the subsequent `if [ -n "$PR_NUMBER" ]` guard passes and the script hits invalid API endpoints with `null` as the PR number.
**Root cause:** Shell guard `[ -n "$VAR" ]` tests string non-emptiness, not JSON null. When jq produces `null` (a valid JSON output, not an empty string), the shell variable is set to the four-character string `"null"` and the guard incorrectly evaluates to true.
**Fix applied:** Changed `--jq '.[0].number'` to `--jq '.[0].number // empty'` so jq emits nothing when the field is absent, and added `&& [ "$PR_NUMBER" != "null" ]` as a belt-and-suspenders guard against the literal string `"null"`.
**Rule added:** When using `gh pr list ... --jq '.[0].field'` in shell scripts, always append `// empty` to the jq expression and guard the variable against the literal string `"null"` before use.

### Lesson 57: Lesson template field names must match the canonical template in LESSONS_LEARNED.md
**File:** `docs/arch/COPILOT_RESOLUTION_SKILL.md`
**What Copilot flagged:** The lesson-entry template in the new Situation B section used `**Rule in CODING_STANDARDS.md:**` but the canonical template (LESSONS_LEARNED.md lines 12–19) uses `**Rule added:**`. Divergent field names break searchability and grep patterns that look for consistent headings.
**Root cause:** The template was written ad hoc in the skill file without cross-checking the established template in LESSONS_LEARNED.md.
**Fix applied:** Changed `**Rule in CODING_STANDARDS.md:** [existing rule name, or "New rule added: [text]"]` to `**Rule added:** [text of new rule or existing rule name this reinforces]` to match the canonical field name.
**Rule added:** Any lesson-entry template written outside LESSONS_LEARNED.md must use identical field names to the canonical template — verify against the `## Template for future entries` block before publishing.

### Lesson 58: File references in documentation must use full repo-relative paths
**File:** `docs/arch/CODING_STANDARDS.md`
**What Copilot flagged:** The new CI Failures checklist section referenced `LESSONS_LEARNED.md` without the repo-relative prefix, violating the rule already in CODING_STANDARDS.md: "File references in Markdown docs must use the full repo-relative path."
**Root cause:** The rule exists as a checklist item but was not applied when writing the new section in the same file — Claude Code did not re-check the checklist against the new content before committing.
**Fix applied:** Updated both bare `LESSONS_LEARNED.md` references to `docs/arch/LESSONS_LEARNED.md`.
**Rule added:** Existing rule reinforced: "File references in Markdown docs must use the full repo-relative path, not just the filename."

---

## PR #78 — feat(mobile): Phase C home feed (follow-up fixes)

### Lesson 59: Authenticated bootstraps must be gated on auth status
**File:** `apps/citizen-mobile/app/(app)/_layout.tsx`, `apps/citizen-mobile/src/lib/pushBootstrap.ts`
**What Copilot flagged:** When guest mode was added to the (app) layout, `usePushBootstrap()` and `initOfflineQueue()` were left running unconditionally. Both depend on a valid session — push registration hits an `[Authorize]` endpoint, and the offline queue's flush drops queued writes on 401. Running them for guests produces wasted auth churn and silent data loss.
**Root cause:** When the auth guard was relaxed to allow guests, the side-effects that previously sat behind the redirect were not re-gated. Ungating one thing (the redirect) silently ungated everything else mounted in the same component.
**Fix applied:** `usePushBootstrap` now accepts an `enabled` flag and no-ops when false; `_layout.tsx` passes `authState.status === 'authenticated'` and only invokes `initOfflineQueue()` from a `useEffect` guarded on the same flag. The unused `useRouter`/`router` left over from the removed redirect were also deleted.
**Rule added:** When relaxing an auth guard to allow guest access, audit every side-effect mounted in that component (hooks, effects, queries) and gate any that hit authenticated endpoints or assume an authed session on `status === 'authenticated'`.

### Lesson 60: React Query calls to authenticated endpoints must set `enabled` for guest mode
**File:** `apps/citizen-mobile/app/(app)/home.tsx`
**What Copilot flagged:** In guest mode, the `['localities','followed']` query called `/v1/localities/followed` without tokens, triggering the API client's 401 refresh flow and leaving `followsLoaded` false (because `setFollowedLocalities` was never invoked). Result: no `NoFollowsState` / calm state and a blank feed.
**Root cause:** The query was added before guest mode existed and was never revisited when guest browsing was introduced.
**Fix applied:** Added `enabled: isAuthenticated` to the query, and a `useEffect` that explicitly calls `setFollowedLocalities([])` when not authenticated so `followsLoaded` flips to true and the NoFollowsState renders for guests.
**Rule added:** Any `useQuery` against an authenticated endpoint must set `enabled` based on auth status, and any consumer of `followsLoaded`-style "loaded" flags must be explicitly transitioned to the loaded state on the unauthenticated branch.

### Lesson 61: Debounced async searches need cleanup + stale-response guards
**File:** `apps/citizen-mobile/app/(app)/home.tsx`
**What Copilot flagged:** The locality search used `setTimeout` with no unmount cleanup, and async responses could resolve out of order — an older query could overwrite newer results, and a stale `setSearching(false)` could fire after unmount. Additionally, on `result.ok === false` the UI kept showing previous results for a different query.
**Root cause:** Standard "naïve debounce" pattern copied without considering React lifecycle or out-of-order resolution.
**Fix applied:** Added an `isMountedRef` and a `latestSearchRequestRef` request-id counter. The unmount effect clears the pending timeout; every state update inside the async callback is gated on `isMountedRef.current && requestId === latestSearchRequestRef.current`. On `!result.ok` and on `catch`, results are explicitly cleared so the UI never shows stale results from a previous query.
**Rule added:** Any debounced async operation must (1) clear its pending timer on unmount, (2) tag each request with a monotonically-increasing id and ignore responses whose id is no longer current, and (3) explicitly handle the error branch — either clear the displayed data or surface an error state — never silently leave stale results.

### Lesson 62: Selectable list items must be filtered to what the selection API actually accepts
**File:** `apps/citizen-mobile/app/(app)/home.tsx`
**What Copilot flagged:** The locality sheet rendered both followed localities and remote search results with the same `onSelectLocality` handler, but `setActiveLocalityId` only accepts ids that exist in `followedLocalities`. Tapping a search result that wasn't already followed silently did nothing, making the sheet appear broken.
**Root cause:** The selection contract (only followed ids accepted) was implicit in the context implementation and not enforced at the UI layer when search results were added.
**Fix applied:** Each row computes `isFollowed` against the followed list; non-followed rows render with reduced opacity, are `disabled`, and report `accessibilityState.disabled`. Follow-then-select for search results is deferred to Phase G (wards settings) where the follow API will be wired.
**Rule added:** When a UI surfaces items from multiple sources (local + remote) into the same selection handler, the items the handler cannot act on must be visually and behaviourally disabled (`disabled`, opacity, `accessibilityState.disabled`) — never silently no-op on tap.

### Lesson 63: Do not use context "loaded" flags for readiness gating when the flag is permanently set by a prior auth state
**File:** `apps/citizen-mobile/app/(app)/home.tsx`
**What Copilot flagged:** `setFollowedLocalities([])` in the unauthenticated branch permanently sets `followsLoaded = true` in `LocalityContext`. On a guest→authenticated transition, `followsLoaded` is already `true` while the authed follows query is still in flight, so `hasNoFollows` and `localityStateText` briefly reflect the empty-follows state even if the user has follows.
**Root cause:** `followsLoaded` is a one-way latch — it flips to `true` and never resets. Using it as a readiness guard across auth transitions assumes `LocalityProvider` is remounted when auth changes, which it is not.
**Fix applied:** Derived a local `followsReady` flag from React Query state — `!isAuthenticated || localitiesQuery.isSuccess || localitiesQuery.isError` — and used it instead of `followsLoaded` for `hasNoFollows` and `localityStateText` gating. Guests are always "ready" (no query runs); authenticated users are only "ready" after the query settles.
**Rule added:** When a context holds a one-way "loaded" latch, do not use it as a readiness guard in components that straddle auth transitions. Derive readiness from the React Query `isSuccess`/`isError` state of the relevant query so the flag resets naturally when the auth state changes.

---

## Template for future entries

Copy this block when adding a new lesson:

## PR #N — title

### Lesson N: short description
**File:** `path/to/file`
**What Copilot flagged:** exact description of the comment
**Root cause:** why did Claude Code generate this incorrectly?
**Fix applied:** what changed in the code
**Rule added:** which checklist item covers this now

---

## PR #79 — Phase D cluster detail screen redesign

### Lesson 1: Unused `ActivityIndicator` import after Submit button refactor
**File:** `apps/citizen-mobile/app/(app)/clusters/[id].tsx`
**What Copilot flagged:** `ActivityIndicator` was still imported from `react-native`
even though the loading UI moved into the shared `Button` component during the
redesign, leaving a dangling import that would trip `noUnusedLocals`.
**Root cause:** When swapping inline `<ActivityIndicator>` for `<Button loading>`,
the corresponding import line was not pruned.
**Fix applied:** Removed `ActivityIndicator` from the `react-native` import list.
**Rule added:** Pre-Commit Checklist → TypeScript → "after refactoring a JSX element
away, search the file for its old import and prune it."

### Lesson 2: Unused `OfficialPostResponse` type import after row component swap
**File:** `apps/citizen-mobile/app/(app)/clusters/[id].tsx`
**What Copilot flagged:** `OfficialPostResponse` was still imported, but the local
`OfficialPostCard` component (which used it) had been replaced with the shared
`OfficialUpdateRow`, which takes individual props instead.
**Root cause:** Same refactor pattern as Lesson 1 — replacing a sub-component
without pruning its now-orphaned type import.
**Fix applied:** Removed `OfficialPostResponse` from the type import.
**Rule added:** Same as Lesson 1 — applies to type imports as well as runtime imports.

### Lesson 3: Stale "session-scoped participation" comment after API echo added
**File:** `apps/citizen-mobile/app/(app)/clusters/[id].tsx`
**What Copilot flagged:** The header comment claimed "Participation state is
session-scoped (API does not echo it back)" even though `ClusterResponse` now
includes `myParticipation` and the screen reads `cluster.myParticipation`.
**Root cause:** Doc-comment was carried over from an earlier version of the
contract and not updated when the server-echoed `myParticipation` field landed.
**Fix applied:** Rewrote the header comment to clarify that the server is the
source of truth and the local state is only a button-highlight UX hint between
mutate and refetch.
**Rule added:** Pre-Commit Checklist → Comments → "when API contract changes,
grep for any header/inline comment that describes the old behavior and update it
in the same commit as the contract change."

### Lesson 4: `requireAuth` could stack multiple `(auth)/phone` screens on repeat taps
**File:** `apps/citizen-mobile/app/(app)/clusters/[id].tsx`
**What Copilot flagged:** `requireAuth` called `router.push('/(auth)/phone')` on
every unauthenticated participation attempt with no guard, so repeated taps
could push multiple phone screens onto the navigation stack.
**Root cause:** The auth gate was written as a stateless check with no protection
against rapid double-taps or successive button presses while still unauthenticated.
**Fix applied:** Added a `navigatingToAuthRef` flag that suppresses additional
pushes once a navigation is already in flight, and is cleared by a `useEffect`
when `authState.status` flips to `'authenticated'`.
**Rule added:** Pre-Commit Checklist → Navigation → "any imperative `router.push`
inside a callback that can fire repeatedly must be guarded against stacking
duplicate destinations."
