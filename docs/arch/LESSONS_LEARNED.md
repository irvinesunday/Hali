# Hali — Lessons Learned from Copilot Reviews
#
# This file is append-only. Never delete entries.
# After every PR that generates Copilot review comments:
#   1. Fix the issue in code
#   2. Add an entry here in the same commit that fixes it
#   3. Add or update the corresponding rule in CODING_STANDARDS.md
#
# Format:
#   ## PR #N — short description
#   **File:** path/to/file.cs
#   **What Copilot flagged:** exact description
#   **Root cause:** why it happened
#   **Fix applied:** what was changed
#   **Rule added to CODING_STANDARDS.md:** which checklist item this maps to

---

## PR #69 — OpenAPI v0.5.0 + app_feedback migration

### Lesson 1: Tabs instead of 4-space indentation (CI failure)
**File:** `src/Hali.Api/Controllers/FeedbackController.cs`
**What Copilot flagged:** WHITESPACE errors on lines 17–24, CI exited with code 2
**Root cause:** Claude Code generated the file with tab characters. The project
enforces 4-space indentation via `.editorconfig`. `dotnet format` detected
8 violations and blocked the CI pipeline.
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

## Template for future entries

Copy this block when adding a new lesson:

## PR #N — title

### Lesson N: short description
**File:** `path/to/file`
**What Copilot flagged:** exact description of the comment
**Root cause:** why did Claude Code generate this incorrectly?
**Fix applied:** what changed in the code
**Rule added to CODING_STANDARDS.md:** which checklist item covers this now
