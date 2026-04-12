# PR Quality Gates

> Mandatory checks before requesting review on any PR.
> Each gate maps to a recurring class of review failure.
> Run these after all commits are done, before requesting review.

---

## G1 — Build clean
- [ ] `dotnet build` zero warnings (C# changes)
- [ ] `dotnet format --verify-no-changes` passes (C# changes)
- [ ] `npx tsc --noEmit` zero errors (TypeScript changes)
- [ ] `dotnet test` zero failures

## G2 — API contract integrity
- [ ] Every new/changed endpoint reflected in `02_openapi.yaml`
- [ ] Every `[ProducesResponseType]` has a code path that returns it
- [ ] Error codes follow `<category>.<reason>` naming (e.g., `validation.locality_unresolved`)
- [ ] Enum wire values match OpenAPI `enum` arrays exactly

## G3 — Test integrity
- [ ] No test methods removed vs. base branch: `git diff <base-branch>...HEAD -- tests/`
- [ ] New domain logic has unit tests
- [ ] Assertions match test names (`Assert.Empty` not `Assert.NotNull` for emptiness)

## G4 — Security surface
- [ ] No request-derived values in structured log templates
- [ ] No hardcoded credentials in design-time factories or configuration
- [ ] `[AllowAnonymous]` endpoints do not accept user-controlled filter
  params that should require authentication

## G5 — Accessibility (mobile PRs)
- [ ] All accessibility props preserved when migrating to shared components
- [ ] `React.memo` comparators include every rendered field

## G6 — Documentation alignment
- [ ] Interface XML docs match implementation (exception types, return types)
- [ ] PR description accurately describes the diff scope

## G7 — Review findings resolved
- [ ] All Copilot review comments are fixed, replied to with rationale,
  or explicitly deferred
- [ ] All CodeQL / code-scanning alerts introduced by the PR are resolved
  or have a documented suppression rationale in the PR thread
- [ ] Intentionally deferred findings that represent real work are logged
  as GitHub issues (with a link in the PR thread) before merge —
  do not let deferred items disappear without a tracking artifact
