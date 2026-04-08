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

## Template for future entries

Copy this block when adding a new lesson:

## PR #N — title

### Lesson N: short description
**File:** `path/to/file`
**What Copilot flagged:** exact description of the comment
**Root cause:** why did Claude Code generate this incorrectly?
**Fix applied:** what changed in the code
**Rule added:** which checklist item covers this now
