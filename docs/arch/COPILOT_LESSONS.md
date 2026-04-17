# Copilot Lessons — High-Signal Recurring Patterns

> Patterns Copilot has flagged multiple times across PRs.
> Read before opening any PR. Each entry names the standing rule
> where the fix is encoded — go there for the full requirement.
>
> This file is intentionally short. Do not duplicate rules already
> explained in `docs/arch/CODING_STANDARDS.md`.

---

## 1. Log-forging via request-derived values
Copilot and CodeQL flag `Request.Path`, `Request.Method`, headers, and
correlation IDs flowing into structured logs. This is the single most
common finding (12+ CodeQL alerts across multiple PRs). The fix is always
the same: use `context.GetEndpoint()?.DisplayName` or
`ObservabilityEvents.SanitizeForLog(...)`.
-> CODING_STANDARDS: C# Conventions > Logging safety

## 2. OperationCanceledException swallowed by catch-all
Exception middleware and service catch blocks that catch `Exception`
without excluding `OperationCanceledException` turn client disconnects
into 500s. Flagged repeatedly across multiple PRs.
-> CODING_STANDARDS: C# Conventions > Exception handling

## 3. Accessibility props dropped on component extraction
When an inline element is replaced with a shared component, accessibility
attributes silently disappear. Flagged 15+ times across multiple mobile
PRs. Diff the old element before deleting it.
-> CODING_STANDARDS: Pre-commit checklist > React Native / TypeScript (citizen-mobile)

## 4. Tests lost during merge conflict resolution
Merge resolution in test files can silently delete test methods or weaken
assertions. This has caused test loss before. Always diff `tests/` against
the base branch after conflict resolution.
-> PR_QUALITY_GATES: G3

## 5. Enum serialization with ToLowerInvariant()
`.ToString().ToLowerInvariant()` on PascalCase enums produces wrong wire
values (e.g., `possiblerestoration`). This pattern has recurred.
-> CODING_STANDARDS: Enum serialization rules

## 6. DbContext used in Task.WhenAll
EF Core DbContext is not thread-safe. Parallel queries on a shared context
corrupt state silently. This pattern has been caught before and should be
treated as a blocking issue.
-> CODING_STANDARDS: C# Conventions > EF Core

## 7. Interface docs diverge from implementation
When a service changes which exception it throws, the interface XML docs
stay stale. Copilot catches the mismatch. Update docs in the same commit.
-> CODING_STANDARDS: C# Conventions > XML documentation

## 8. Integration test schema bootstrap drifts from EF model
Adding a persisted column via EF model/migration without updating the raw
SQL in `HaliWebApplicationFactory.EnsureSchemaAsync()` causes integration
tests to fail with 500 errors — the test DB lacks the column EF expects.
This is invisible in unit tests and only surfaces in integration CI.
-> CODING_STANDARDS: Pre-commit checklist > Migrations
