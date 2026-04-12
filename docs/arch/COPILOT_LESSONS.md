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
correlation IDs flowing into structured logs. This was the single most
common finding (12+ CodeQL alerts, 5 PRs). The fix is always the same:
use `GetEndpoint()?.DisplayName` or `SanitizeForLog()`.
-> CODING_STANDARDS: C# Conventions > Logging safety

## 2. OperationCanceledException swallowed by catch-all
Exception middleware and service catch blocks that catch `Exception`
without excluding `OperationCanceledException` turn client disconnects
into 500s. Flagged in PRs #92, #104, #106.
-> CODING_STANDARDS: C# Conventions > Exception handling

## 3. Accessibility props dropped on component extraction
When an inline element is replaced with a shared component, accessibility
attributes silently disappear. Flagged 15+ times across 5 mobile PRs
(#74, #78, #79, #81, #82). Diff the old element before deleting it.
-> CODING_STANDARDS: React Native / TypeScript checklist

## 4. Tests lost during merge conflict resolution
Merge resolution in test files can silently delete test methods or weaken
assertions. Two tests lost in PR #106. Always diff `tests/` against the
base branch after conflict resolution.
-> PR_QUALITY_GATES: G3

## 5. Enum serialization with ToLowerInvariant()
`.ToString().ToLowerInvariant()` on PascalCase enums produces wrong wire
values (e.g., `possiblerestoration`). Flagged in PR #94.
-> CODING_STANDARDS: Enum serialization rules

## 6. DbContext used in Task.WhenAll
EF Core DbContext is not thread-safe. Parallel queries on a shared context
corrupt state silently. Caught in PR #98, immediately reverted.
-> CODING_STANDARDS: C# Conventions > EF Core

## 7. Interface docs diverge from implementation
When a service changes which exception it throws, the interface XML docs
stay stale. Copilot catches the mismatch. Update docs in the same commit.
-> CODING_STANDARDS: C# Conventions > XML documentation
