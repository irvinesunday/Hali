# PR Quality Gates

> Mandatory checks before requesting review on any PR.
> Each gate maps to a recurring class of review failure.
> Run these after all commits are done, before requesting review.

---

## G1 — Build clean
- [ ] `dotnet build` no new warnings vs. the current baseline (C# changes)
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

### G4.a — Security hygiene (every PR)
- [ ] No request-derived values in structured log templates
- [ ] No hardcoded credentials in design-time factories or configuration
- [ ] `[AllowAnonymous]` endpoints do not accept user-controlled filter
  params that should require authentication

### G4.b — Security review checklist (PRs touching security-sensitive surface)

When a PR touches any of the areas below, mandatory security review is
required before merge. Check each item that applies; write an explicit
"N/A — does not touch X" in the PR description when an area is untouched
so the reviewer sees the scan was performed.

Authentication and identity
- [ ] OTP / SMS / login flow changes preserve rate limiting, lockout, and
      idempotency guarantees
- [ ] JWT audience / issuer / key rotation changes are reflected across
      runtime config, CI env, `.env.example`, and test constants
- [ ] Refresh token rotation + theft detection
      (`replaced_by_token_hash`) semantics are preserved; no plaintext
      storage, no missing revocation
- [ ] Session / cookie / bearer-token surface changes on any web app use
      `httpOnly` + `Secure` + `SameSite=Lax` and never `localStorage`

Authorization and tenancy
- [ ] Every new authenticated endpoint has an explicit policy / scope
      attribute; `[AllowAnonymous]` is stated explicitly when intended
- [ ] Institution, institution-admin, and ops endpoints enforce
      jurisdiction / scope server-side — no reliance on client-supplied
      membership or tenant claims
- [ ] Cross-tenant data access paths (list queries, cluster lookup,
      participation) filter by the authenticated actor's institution /
      locality scope before returning results

Secrets, config, and supply chain
- [ ] No secrets, tokens, API keys, or private URLs committed in source,
      config, tests, fixtures, or CI workflows
- [ ] New environment variables are documented in `.env.example` with
      safe placeholder values (never a real secret)
- [ ] New third-party dependencies are justified in the PR description;
      transitive license / security signal checked

Rate limiting and abuse
- [ ] Any new public or authenticated endpoint that accepts writes has a
      rate limit or justification for why not
- [ ] Rate-limit keys are bound to a stable actor identity (account id,
      device id, or IP fallback) — not to a spoofable client header
- [ ] 429 behaviour is exercised by at least one test (or an explicit
      note that the limit is enforced elsewhere)

PII, logging, and audit
- [ ] Phone numbers, OTP codes, raw device identifiers, and free-text
      signal content are never written to logs unredacted
- [ ] Any new user-supplied value that must be logged is sanitised via
      `ObservabilityEvents.SanitizeForLog(...)`
- [ ] Any new privileged action writes both a structured log entry and
      an audit-trail record (or documents why one is unnecessary)

Tenant and scope boundaries
- [ ] Citizen public responses remain free of CIVIS internals
      (`civis_score`, `wrab`, `sds`, `macf`, `raw_confirmation_count`)
- [ ] Citizen public responses remain free of `account_id` / `device_id`
- [ ] Institution responses do not leak citizen identity or cross-scope
      clusters

Error envelope and information disclosure
- [ ] Error responses use the canonical typed envelope — no stack traces,
      no raw exception messages leaked
- [ ] Authentication failures do not distinguish "missing token" vs
      "invalid token" vs "expired token" in the response body (avoid
      oracle side-channels)

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
