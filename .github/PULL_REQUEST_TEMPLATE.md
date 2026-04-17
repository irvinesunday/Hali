## Summary

Describe what this PR does in plain language.

## Changes

-
-
-

## Issue linkage

Closes #

## Phase / Workstream

State the current build phase and workstream this PR belongs to
(e.g. Mobile Phase B, Phase 2 institution dashboard, repo hygiene, etc.)

## Authorization impact

State whether this PR changes:
- roles or scope enforcement
- JWT claim handling
- cross-institution boundaries
- admin-only behavior

If none, write: None

## Security considerations

State whether this PR changes:
- auth/session behavior
- cookies
- CSRF
- TOTP / MFA
- magic links
- secrets/config
- logging of sensitive data

If none, write: None

## Observability impact

State whether this PR adds or changes:
- metrics
- logs
- traces
- alerts

If none, write: None

## OpenAPI updated

- [ ] Yes
- [ ] No
- [ ] Not applicable

If yes, summarize changes.

## Authorization coverage matrix updated

- [ ] Yes
- [ ] No
- [ ] Not applicable

If No or N/A, explain why.

## Tests added

-
-
-

## How to test

List the steps or commands needed to verify this PR's changes.

## Test execution

List commands run (e.g. `dotnet test --project tests/Hali.Tests.Unit`)

## Copilot findings

_Claude Code must reply to all Copilot review comments via the GitHub API
and resolve threads before this PR is considered complete._

| Finding | Classification | Action taken | Follow-up issue |
|--------|---------------|-------------|-----------------|

Valid classifications:
- VALID AND ALIGNED
- VALID BUT OUT OF SCOPE
- VALID BUT DEFERRED
- VALID BUT MISALIGNED
- NOT VALID

## Checklist

- [ ] Scope matches issue
- [ ] No unrelated changes
- [ ] Contract-first integrity preserved
- [ ] Authorization boundaries verified
- [ ] Cross-institution leakage tested
- [ ] OpenAPI updated if required
- [ ] Authorization coverage matrix updated if required
- [ ] Tests added/updated
- [ ] CI passed
- [ ] All Copilot comments replied to via GitHub API and resolved or tracked
