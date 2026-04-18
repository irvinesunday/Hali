# Hali Security Posture Baseline

This document is the canonical security posture for Hali across citizen,
institution, institution-admin, and future Hali-ops surfaces. It captures the
**current** posture (what is implemented in the codebase right now) and the
**target** posture (what new surfaces must satisfy before they can ship to
pilot users).

When the current posture and target posture differ, the gap is listed
explicitly at the end of this document. A gap means work to do, not
permission to ship insecure.

This document is policy, not a PR checklist. The operational checklist is
`docs/arch/PR_QUALITY_GATES.md` — gate G4.b. The two must stay aligned.

Authority hierarchy (per `CLAUDE.md`): this document sits below `CLAUDE.md`
and `Hali_Platform_Reconciliation_v1.md` but above the session- and
phase-specific files. When a specific implementation doc (e.g.
`07_auth_implementation.md`) conflicts with this posture, fix this document
to match the implementation — do not lower the posture.

---

## Scope

This document covers:

1. Authentication and identity
2. Authorization, roles, and scope
3. Token and session handling
4. PII, logging, and audit
5. Secrets and config management
6. Secure defaults for new web apps
7. Auditability principles
8. Known implementation gaps

It does not cover infrastructure posture (Neon, runners, CDN) — that lives
in operations documentation.

---

## 1. Authentication and identity

### Citizen mobile (Phase 1 — implemented)

- Primary factor: SMS OTP via Africa's Talking (`ISmsProvider`).
- OTP codes: 6 digits, 10-minute TTL, max 3 attempts per challenge.
- OTP issuance is rate-limited per destination phone number.
- OTP codes are never logged in structured logs; only the destination hash
  and dispatch outcome are recorded.
- Account is anchored to phone number + device fingerprint
  (`device_fingerprint_hash`). A new device on the same account requires a
  fresh OTP verification.

### Institution operational user (Phase 2 — auth foundation in place; see #197)

- Primary factor: email magic link.
- Second factor: mandatory TOTP, enrolled at first login. No bypass path.
- Magic link tokens are single-use, 15-minute TTL.
- TOTP secrets are encrypted at rest via ASP.NET Data Protection.
- Institution membership is a separate `institution_memberships` row with a
  single `institution_id` and a set of `institution_user_scopes`.

### Institution-admin user (Phase 2 — planned; see #197 + #198)

- Same primary/second factor as institution operational users.
- Step-up posture (planned): privileged actions (role grant, scope grant,
  institution membership edit) require a fresh second-factor challenge
  within the last N minutes (exact value TBD in #197 implementation).
- Admin actions always emit both a structured log entry and an audit row.

### Hali-ops user (Phase 3 — planned)

- Same auth model as institution-admin plus federated SSO when available.
- Posture must be at least as strong as institution-admin. Never weaker.

---

## 2. Authorization, roles, and scope

### Principle: server-side enforcement only

Frontend hiding of UI elements, feature flags that gate visibility, and
client-side role checks are **never** security boundaries. Every protected
action is authorized server-side in the controller or service layer.

### Citizen scope

- Citizens have no role beyond "authenticated account".
- Citizens can only read public cluster data and their own participation
  history.
- A citizen can never read another citizen's `account_id`, `device_id`, or
  raw signal text. This rule also applies to institution responses — the
  citizen identity is internal to the platform.

### Institution scope

- An institution operational user's effective scope is the intersection of:
  (a) their `institution_memberships` row and (b) their
  `institution_user_scopes` rows for that institution.
- Cross-institution data access is forbidden. Query filters must apply the
  actor's institution id server-side before any data leaves the repository.
- Jurisdiction enforcement (which clusters / localities an institution can
  act on) is evaluated server-side on every request — not precomputed and
  cached on the client.

### Institution-admin scope

- Can grant/revoke operational-user roles and scopes inside its institution.
- Cannot grant Hali-ops access.
- Cannot act on another institution.

### Hali-ops scope

- Read access to all institutions (for support and moderation).
- Write access restricted to infrastructure and platform operations —
  never to citizen data or institution clusters.

### Anonymous endpoints

- `[AllowAnonymous]` is **explicit** on every public endpoint. The absence
  of `[Authorize]` is not sufficient; `[AllowAnonymous]` must be present so
  intent is auditable.
- Anonymous endpoints never accept user-controlled filter parameters that
  should require authentication (e.g. `accountId`, `institutionId`).

---

## 3. Token and session handling

### Access tokens — citizen mobile (implemented)

- JWT HS256, audience `hali-platform`, issuer `hali`.
- 60-minute expiry; clients refresh silently before expiry.
- Signed with a single platform secret from config (`Auth:JwtSecret`).
  Never less than 32 characters; never committed.
- Stored on-device via Expo SecureStore. Never AsyncStorage. Never
  localStorage.

### Refresh tokens — citizen mobile (implemented)

- Stored server-side as SHA-256 hex hashes — never plaintext.
- Fields: `token_hash`, `account_id`, `device_id`, `expires_at`,
  `revoked_at`, `replaced_by_token_hash`, `created_at`.
- 30-day expiry from issue; each use rotates to a new token and sets
  `replaced_by_token_hash` on the previous row.
- **Theft detection:** re-presentation of a rotated token (one whose
  `replaced_by_token_hash` is already set) revokes the entire token family
  for that `account_id` + `device_id`. All outstanding refresh tokens for
  that account/device tuple are marked `revoked_at = now()`.
- Logout revokes the current refresh token. Revocation is explicit, never
  time-only.

### Web sessions — institution + institution-admin + ops (planned — #197)

- Access tokens carried as `httpOnly` + `Secure` + `SameSite=Strict`
  cookies. **Never** localStorage. **Never** JavaScript-readable storage.
- CSRF: state-changing requests require a CSRF token on top of the cookie
  session — implementation pattern defined in #197.
- Session idle timeout: 30 minutes. Hard expiry: 12 hours.
- Step-up auth timestamp attached to the session; privileged actions check
  the timestamp is within policy before executing.

### Key rotation (target)

- JWT signing keys must be rotatable without forcing all mobile clients to
  re-login. Current deployment uses a single key; key-set rotation is
  tracked as a future improvement.

---

## 4. PII, logging, and audit

### PII inventory

The following values are PII and must be handled with the rules in this
section:

- Phone numbers (raw or partial)
- OTP codes (including expired codes)
- Device fingerprint hashes (quasi-identifier)
- Raw signal free-text submitted by the citizen
- Geographic coordinates tied to an account
- Email addresses (institution + ops users)
- TOTP secrets (institution + ops users)

### Redaction rules

- Raw PII is never written to structured logs. Any user-supplied value
  that must appear in a log goes through
  `ObservabilityEvents.SanitizeForLog(value)`.
- Phone numbers in logs must be truncated (e.g. last 4 digits masked) or
  hashed — never raw.
- OTP codes are never logged under any circumstance, including failure
  paths.
- Free-text signal content is never logged. Cluster lookups log the
  `cluster_id` and `locality_id`, not the originating text.
- Request path / method / query strings are never passed raw into log
  templates — use `context.GetEndpoint()?.DisplayName` or the sanitize
  helper. (CodeQL flags this repeatedly; see `COPILOT_LESSONS.md`.)

### Audit trail

- Every privileged action (institution role grant, cluster restoration
  post, institution-admin membership edit, feature-flag flip) writes an
  audit-trail row in addition to the structured log.
- Audit rows are append-only. No update, no delete. Retention is bounded
  by policy, not by product need.
- Citizen actions (signal submit, participation) emit outbox events via
  the outbox pattern (`outbox_events`); they are not audit rows but
  satisfy the replayability requirement in the same transaction.

---

## 5. Secrets and config management

### Never commit

The following must never appear in the repo in any form (source, tests,
fixtures, docs, CI workflows, migrations):

- Real API keys for Anthropic, Africa's Talking, Expo, Google Maps, or
  any other third party
- Database passwords for any environment other than the local dev
  placeholder in `.env.example`
- JWT signing keys
- TOTP secrets
- Session encryption keys
- Service account credentials
- Private URLs pointing at internal infrastructure

### Safe placeholders

- `.env.example` uses obvious placeholder values
  (`replace_with_a_long_random_secret_minimum_32_chars`, empty keys for
  third parties, the local dev password `changeme`). Any new environment
  variable introduced by a PR must be documented here with a placeholder.
- `appsettings.json` contains the **development** defaults only. The
  production deployment overrides every `Auth:*`, `ConnectionStrings:*`,
  and third-party key via environment variables.

### Secret scanning

- `gitleaks` runs in CI (`Secret Scanning (gitleaks)` job). A failure
  blocks merge.
- CodeQL scans for hardcoded credentials in C# and TypeScript sources.

### Rotation

- Any secret suspected of being leaked is rotated immediately.
- JWT signing keys rotate on a schedule (target: every 90 days in
  production) — implementation follows key-set rotation to avoid forcing
  all clients to re-login.

### Data Protection key ring (implemented — #243)

Hali uses ASP.NET Core Data Protection for at-rest encryption of
material that must round-trip through the app (currently: TOTP secrets
for institution + institution-admin users from #197). The key ring
itself — the set of rotating keys that encrypt that material — must
be persisted and protected so it survives process restarts and
replicates across nodes.

- **Key ring storage**: the key ring is persisted to PostgreSQL in the
  `data_protection_keys` table via `HaliDataProtectionDbContext`.
  This is the single storage location across all environments —
  local dev, staging, production — so operators never reason about
  where the ring lives, only how it is protected.
- **At-rest protection**: the ring itself is encrypted with an X.509
  certificate (PFX) loaded from the path in `DataProtection:CertPath`
  (password in `DataProtection:CertPassword`).
- **Production gate**: `DataProtection:CertPath` and
  `DataProtection:CertPassword` MUST be provisioned before institution
  users are onboarded to production. Production fails-fast at startup
  in **both** of these cases — a misconfigured production deployment
  must not silently run unprotected:
  - `CertPath` is configured but the file cannot be loaded (missing
    or invalid)
  - `CertPath` is not configured at all (no fallback — DPAPI is not
    canonical for Production)
- **Non-Production degradation**: dev and staging log an ERROR and
  continue unprotected if the cert is configured but missing, so a
  broken shared mount does not block local work. If the cert is not
  configured at all outside Production, startup logs a WARNING and
  keys are unprotected.
- **Dev-machine escape hatch**: when no cert is configured on Windows,
  the key ring uses DPAPI (user-scoped). DPAPI is **never** acceptable
  for staging or production — operators must provision a cert.
- **KMS evolution**: the provider is designed for swap. A future move
  to AWS KMS / Azure Key Vault / equivalent requires only changing the
  protection provider; no change to the key ring storage
  (`data_protection_keys`) is needed.
- **Secret discipline**:
  - The certificate **password** is never written to any log under
    any condition.
  - The certificate **path** is logged as the filename portion only —
    never the full path — so log aggregation cannot disclose
    deployment-topology signal.
  - The startup WARNING emitted when `CertPath` is unset does NOT
    echo the expected path — an attacker reading the log should not
    learn where the system looks for its cert.
  - PFX / PEM / KEY / P12 files are blocked by `.gitignore`.

---

## 6. Secure defaults for new web apps

Every new surface (institution-web, institution-admin-web, hali-ops-web)
must satisfy the following before it can ship to any pilot user:

### Transport

- HTTPS only. HSTS header on every response with a minimum `max-age` of
  31536000 (1 year).
- TLS 1.2 minimum at the edge. TLS 1.3 preferred.
- No mixed content. All asset URLs must be `https://` or protocol-
  relative behind HTTPS edge.

### Headers

- `Strict-Transport-Security` as above.
- `X-Content-Type-Options: nosniff`.
- `X-Frame-Options: DENY` or a strict `Content-Security-Policy`
  `frame-ancestors` directive.
- `Referrer-Policy: strict-origin-when-cross-origin`.
- `Content-Security-Policy` with explicit `default-src`, `script-src`,
  `style-src`, `img-src`, `connect-src`, `font-src` — no `unsafe-eval`,
  no `unsafe-inline` without a nonce.
- `Permissions-Policy` denying unused browser features (camera,
  microphone, geolocation except where justified).

### Cookies

- Every authentication and session cookie: `httpOnly`, `Secure`,
  `SameSite=Strict`, `Path=/`, scoped to the app's hostname (no
  `.example.com` parent-domain wildcarding).

### Dependencies

- `npm audit` / equivalent gate at CI; high/critical vulnerabilities
  block merge.
- Pinned versions for all direct dependencies. `pnpm-lock.yaml` /
  equivalent committed.

### Content

- Never render un-escaped user-supplied content. Use the framework's
  safe-by-default rendering (React text nodes, not
  `dangerouslySetInnerHTML`).
- Rich text (if ever added) must go through an explicit allowlist
  sanitizer — never a denylist.

---

## 7. Auditability principles

The platform's doctrine of neutrality (`CLAUDE.md` Part 1) makes auditability
a first-class security property, not a compliance afterthought.

- **Every state change is traceable.** The outbox pattern (see
  `docs/arch/06_worker_pipelines.md`) ensures every cluster lifecycle
  transition, participation, and official update emits a durable event in
  the same transaction as the write.
- **No magic state changes.** Every cluster transition carries a reason
  code (`lifecycle.activated_by_civis`, `lifecycle.restoration_confirmed`,
  etc.).
- **Every privileged action writes an audit row.** Privileged means
  anything a non-citizen actor does to affect a citizen-facing surface,
  or anything that changes access control.
- **Audit rows contain enough context to reconstruct the decision.** At
  minimum: actor id, actor type, action name, target id, target type,
  before/after state (for scope grants/revocations), correlation id, and
  timestamp.

---

## 8. Known implementation gaps

These are real gaps between current and target posture. Each should have
(or acquire) a tracking issue.

| Gap | Current | Target | Tracking |
|---|---|---|---|
| JWT issuer drift between runtime default (`hali`) and documented value (`hali-api`) | Runtime and config use `hali`; arch docs declare `hali-api` | Single canonical value everywhere, with a regression guard analogous to the one added for audience in #185 | Follow-up to #185 — to be filed |
| Institution web session hardening | Auth foundation present; web session semantics not finalised | `httpOnly` + `Secure` + `SameSite=Strict`, CSRF, idle + hard timeouts | #197 |
| Authorization policy coverage | Institution routes exist with scope checks; coverage not systematically audited | Every institution + institution-admin route has an explicit policy, and the audit confirms every route appears in the policy matrix | #198 |
| Step-up auth for institution-admin | Not implemented | Privileged actions require fresh second-factor within policy window | Follow-up to #197 — to be filed |
| Web security headers (CSP, HSTS, frame-ancestors) | Not implemented (no web apps shipped yet) | All new web surfaces ship with the header set defined in §6 from day one | Will be part of each institution-web PR |
| Rate limiting coverage | `/v1/feedback` + `/v1/signals/preview` have Redis-backed rate limits; others do not | Every authenticated write endpoint has an explicit rate limit or a documented reason for omission | Rolling — revisit as new write endpoints are added |
| Centralised audit log schema | Outbox covers state changes; admin audit rows exist (`admin_audit_logs`) but are not yet unified across all privileged actions | Single canonical audit write site for all privileged actions with a consistent shape | Follow-up — to be filed as Phase 2 progresses |
| JWT signing key rotation | Single static key via config | Key-set rotation without forcing client re-login | Operational future work |

A gap does not give a PR permission to regress. A new feature that crosses
a gapped area still has to satisfy the target posture **for its own surface**
— e.g. a new web surface ships with full CSP headers from day one even
though older surfaces may not have them yet.

---

## 9. How this document changes

- The posture itself (what we require) changes only via an explicit
  PR that calls out the change in the description.
- A gap closing moves the row from §8 into the corresponding §1–§7
  section.
- A new threat surface (e.g. a new external integration) adds items to
  the relevant section and to `PR_QUALITY_GATES.md` G4.b if review needs
  a checklist item.
