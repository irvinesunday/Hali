# Hali Authorization Coverage Matrix

**Issue:** #198. **Phase:** 2 foundation — produced as the
authorization audit deliverable called out in the issue. Captures
the current authorization posture of every HTTP endpoint in
`src/Hali.Api/Controllers/*.cs`, along with the explicit role/scope
rule, anonymous exposure, and the test-coverage state.

This matrix is enforceable: every new controller action must be
added here in the same PR, with an explicit authorization decision.
Missing an entry is a merge block.

**Cross-references:**
- `docs/arch/SECURITY_POSTURE.md` §2 — the posture this matrix
  verifies conforms to
- `docs/arch/PR_QUALITY_GATES.md` G4.b — the PR-time checklist that
  references this matrix

---

## 1. Matrix

Columns:

- **Route** — HTTP method + path (relative to `v1/`).
- **Controller** — the `*.cs` file the action lives in.
- **Decorator** — the authorization attribute that governs the
  action (`[Authorize]`, `[Authorize(Roles = …)]`, `[AllowAnonymous]`).
- **Policy / scope** — the effective authorization rule enforced,
  including any server-side scope check that happens inside the
  action body (not only at the attribute level).
- **Tested?** — the state of the happy-path / forbidden-path
  integration tests for this endpoint. `✓` means both exist; `~`
  means at least one is missing; `✗` means no integration coverage.

### Auth routes

| Route | Controller | Decorator | Policy / scope | Tested? |
|---|---|---|---|---|
| `POST /v1/auth/otp` | AuthController | `[AllowAnonymous]` (inherited absence) | Rate-limited per destination; no actor required | ✓ |
| `POST /v1/auth/verify` | AuthController | `[AllowAnonymous]` | OTP code validates against stored challenge | ✓ |
| `POST /v1/auth/refresh` | AuthController | `[AllowAnonymous]` | Server-side hashed-refresh-token rotation + theft detection | ✓ |
| `POST /v1/auth/logout` | AuthController | `[Authorize]` | Revokes caller's current refresh token | ✓ |
| `POST /v1/auth/institution/setup` | AuthController | `[AllowAnonymous]` | Invite token validated server-side | ~ (happy path only) |

### Clusters routes

| Route | Controller | Decorator | Policy / scope | Tested? |
|---|---|---|---|---|
| `GET /v1/clusters/{id}` | ClustersController | `[AllowAnonymous]` | Returns public projection only; CIVIS internals never exposed | ✓ |
| `POST /v1/clusters/{id}/participation` | ClustersController | `[Authorize]` | `accountId` from JWT; enforces per-cluster duplicate rules | ✓ |
| `POST /v1/clusters/{id}/context` | ClustersController | `[Authorize]` | `accountId` from JWT; 2-minute context edit window enforced | ✓ |
| `POST /v1/clusters/{id}/restoration-response` | ClustersController | `[Authorize]` | Citizen participation vote; carries `deviceHash`; rate limited per device | ✓ |

### Signals routes

| Route | Controller | Decorator | Policy / scope | Tested? |
|---|---|---|---|---|
| `POST /v1/signals/preview` | SignalsController | `[AllowAnonymous]` | Rate-limited per client fingerprint; NLP boundary | ✓ |
| `POST /v1/signals/submit` | SignalsController | `[Authorize]` | `accountId` from JWT; idempotency key required | ✓ |

### Home, feedback, places, localities

| Route | Controller | Decorator | Policy / scope | Tested? |
|---|---|---|---|---|
| `GET /v1/home` | HomeController | `[AllowAnonymous]` | Public home feed; anonymous allowed; follows-scope applied server-side when actor is authenticated | ✓ |
| `POST /v1/feedback` | FeedbackController | `[AllowAnonymous]` | Rate-limited per client fingerprint; 429 on violation | ✓ |
| `GET /v1/places/search` | PlacesController | `[AllowAnonymous]` | Proxies search; no user state | ~ |
| `GET /v1/places/reverse` | PlacesController | `[AllowAnonymous]` | Same | ~ |
| `GET /v1/localities/followed` | LocalitiesController | `[Authorize]` | `accountId` from JWT; lists the caller's followed localities | ✓ |
| `PUT /v1/localities/followed` | LocalitiesController | `[Authorize]` | Max-5-wards enforced server-side | ✓ |
| `GET /v1/localities/wards` | LocalitiesController | `[AllowAnonymous]` | Public ward listing | ~ |
| `GET /v1/localities/search` | LocalitiesController | `[AllowAnonymous]` | Public search | ~ |
| `GET /v1/localities/resolve-by-coordinates` | LocalitiesController | `[AllowAnonymous]` | Public reverse-geocode | ✓ |

### Users, devices

| Route | Controller | Decorator | Policy / scope | Tested? |
|---|---|---|---|---|
| `GET /v1/users/me` | UsersController | `[Authorize]` (class-level) | Reads caller's own account | ✓ |
| `PUT /v1/users/me/notification-settings` | UsersController | `[Authorize]` (inherited) | Writes caller's own notification settings | ✓ |
| `POST /v1/devices/push-token` | DevicesController | `[Authorize]` | Registers Expo push token for caller + device | ✓ |

### Official posts

| Route | Controller | Decorator | Policy / scope | Tested? |
|---|---|---|---|---|
| `POST /v1/official-posts` | OfficialPostsController | `[Authorize(Roles = "institution")]` | `institution_id` from JWT (no header fallback); `localityId` / `corridorName` validated against caller's jurisdiction server-side; supports `isRestorationClaim=true` + `relatedClusterId`; validates optional `responseStatus` (live_update only) + `severity` (scheduled_disruption only) | ✓ |

### Institution operational

| Route | Controller | Decorator | Policy / scope | Tested? |
|---|---|---|---|---|
| `GET /v1/institution/overview` | InstitutionController | `[Authorize(Roles = "institution")]` (class-level) | `institution_id` from JWT (ForbiddenException when absent); optional `areaId` validated against caller's jurisdictions server-side | ✓ |
| `GET /v1/institution/signals` | InstitutionController | class-level | `institution_id` from JWT; `state` filter validated against canonical enum; locality scope applied server-side before any rows leave the repository | ✓ |
| `GET /v1/institution/signals/{clusterId}` | InstitutionController | class-level | `institution_id` from JWT; returns 404 for out-of-scope clusters to prevent cross-institution existence probe | ✓ |
| `GET /v1/institution/areas` | InstitutionController | class-level | `institution_id` from JWT; rows bounded to `institution_jurisdictions` owned by the caller | ✓ |
| `GET /v1/institution/activity` | InstitutionController | class-level | `institution_id` from JWT; activity feed bounded to caller's localities | ✓ |

### Admin

| Route | Controller | Decorator | Policy / scope | Tested? |
|---|---|---|---|---|
| `POST /v1/admin/institutions` | AdminController | `[Authorize(Roles = "admin")]` (class-level) | Admin-only; creates institution + invite | ~ (forbidden path for citizen + institution roles tested in `ForbiddenRoleEnvelopeTests`; happy path not integration-tested due to DB setup cost) |
| `DELETE /v1/admin/institutions/{id}/access` | AdminController | inherited | Admin-only; blocks accounts + revokes refresh tokens | ~ (forbidden path tested for citizen role; happy path not integration-tested) |

### Feature flags (new — #194)

| Route | Controller | Decorator | Policy / scope | Tested? |
|---|---|---|---|---|
| `GET /v1/feature-flags` | FeatureFlagsController | `[AllowAnonymous]` | Server-only flags never returned regardless of role; actor-type derived from JWT if present, otherwise `"anonymous"` | ✓ |

---

## 2. Phase 2 planned additions (from Phase 1.5 synthesis)

Every planned endpoint must land in this matrix with both happy-path
and forbidden-path integration tests at merge time. The contracts are
already defined in `docs/arch/hali_institution_backend_contract_implications.md`:

| Route | Planned decorator | Planned policy |
|---|---|---|
| `/v1/institution-admin/*` | `[Authorize(Roles = "admin")]` (or a new `institution_admin` role — decided in #196) | Institution-admin scope, single institution |

The five `/v1/institution/*` operational routes and the field additions
on `/v1/clusters/{id}` + `/v1/official-posts` landed with #195 and are
recorded above. The institution-admin surface remains pending on #196.

---

## 3. Audit findings

State of the current authorization posture as of this audit:

### 3.1 Positive findings

- Every non-anonymous endpoint has an explicit `[Authorize]` (or
  `[Authorize(Roles = …)]`) decorator.
- `[AllowAnonymous]` is **explicit** on every public endpoint —
  anonymous access is never implied by absence of `[Authorize]`.
- The `POST /v1/official-posts` controller does NOT read the
  `institution_id` from a client-supplied header — it reads it from
  the JWT claim (this was a past bypass that was closed).
- Refresh-token rotation + theft detection is implemented and tested
  (family revocation on re-use of a rotated token).
- Feature-flag endpoint (`/v1/feature-flags`) returns only
  `ClientVisible` flags regardless of role, and is tested both
  anonymously and with a citizen token.

### 3.2 Gaps flagged for follow-up

- **`POST /v1/auth/institution/setup`** — happy path is tested; no
  explicit test for an invalid / expired invite token. Add negative
  tests in the Phase 2 auth-hardening work.
- **Admin-route happy paths** — `POST /v1/admin/institutions` and
  `DELETE /v1/admin/institutions/{id}/access` have forbidden-path
  integration coverage (via `ForbiddenRoleEnvelopeTests`) but the
  happy paths for these admin writes are unit-tested only. The
  Phase 2 admin-routes implementation (#196) extends integration
  coverage.
- **Places + ward listing + locality search** —
  `[AllowAnonymous]` is intentional, but these lack negative
  integration tests confirming that crafted query parameters do
  not extend scope beyond the caller's role. Add low-cost tests
  when the next locality work touches those endpoints.
- **`institution_admin` role distinction** — the codebase currently
  uses a single `admin` role for the Hali-ops/admin surface. Phase 2
  #196 introduces a distinct `institution_admin` role; when it
  lands, every admin-only endpoint must choose the narrower role
  and this matrix must be updated.
- **CSRF posture for future web surfaces** — not a current gap
  (no web surface in production yet) but must land as part of #197
  before institution-web ships, and must be reflected here at the
  same time.

### 3.3 Scope discipline

This audit does not add or remove integration tests. The posture
verification it produces is the matrix itself; every claim in the
matrix is falsifiable against the test fixture file path it cites.
Test additions for the gaps in §3.2 are the work of their
respective follow-up issues (#195, #196, #197), not this audit.

---

## 4. How to keep this matrix current

- Every PR that adds a controller action must add a matrix row in
  the same PR. Reviewers reject PRs that skip this.
- Every PR that removes a controller action must delete its row.
- Changing an existing action's authorization decorator is a
  matrix-edit-required change — the diff to this document is
  review-visible.
- This matrix does NOT replace the per-endpoint integration tests;
  it is the _audit_, not the test. Matrix entries point at the
  existing test files or call out their absence.

---

## 5. Relationship to `docs/arch/SECURITY_POSTURE.md`

This matrix is the operational verification of §2 ("Authorization,
roles, and scope") of the security posture doc. When the two
disagree, the security posture is authoritative and this matrix
gets updated to conform.
