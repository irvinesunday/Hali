# Hali Platform Reconciliation & Gap Resolution
**Version 1.0 — April 2026**
*Canonical resolution document covering all conflicts, schema gaps, cross-surface data exchange, and undiscovered issues across the full build pack and dashboard architecture pack.*

---

## How to use this document

This document is authoritative where it conflicts with earlier docs. Everything in the original build pack and dashboard architecture pack stands unless this document explicitly overrides it. When implementing, read this document alongside those packs — not instead of them.

Sections are ordered by severity: conflicts first, then schema gaps, then surface-specific issues, then the data exchange architecture. The final section is a prioritised implementation order.

---

## Part 1 — Conflicts Resolved

### 1.1 Phase Boundary Declaration

**The conflict:** `claude.md` and `mvp_locked_decisions.md` both mark the admin portal and institution dashboards as out of scope for Phase 1. The dashboard architecture pack introduces all three web dashboards as locked production surfaces with no phase assignment.

**Resolution — locked:**

| Phase | Scope |
|---|---|
| **Phase 1** | Citizen mobile app (React Native + Expo). All items in `claude.md` Phase 1 scope. |
| **Phase 2** | Institution Operations Dashboard + Institution Admin Dashboard (Next.js). Backend institution and institution-admin route families. Institution role model and schema. |
| **Phase 3** | Hali Ops Admin Dashboard (Next.js). Ops route family. Trust/integrity surfaces. Advanced analytics. |

Phase 1 must reach a shippable, tested state before Phase 2 begins. Phase 2 must reach institutional pilot readiness before Phase 3 begins.

The dashboard architecture pack defines the **target architecture** for Phases 2 and 3. It is not Phase 1 scope. All document references in the pack are valid and frozen — they just have a phase gate.

`claude.md` remains the Phase 1 implementation authority. The dashboard pack is the Phase 2–3 implementation authority.

---

### 1.2 Auth Route Names — Canonical Resolution

**The conflict:** `02_openapi.yaml` defines `/v1/auth/request-otp` and `/v1/auth/verify-otp`. Every other document (`claude.md`, `openapi_patch_checklist.md`, `mobile_screen_inventory.md`, `mvp_locked_decisions.md`) uses `/v1/auth/otp` and `/v1/auth/verify`.

**Resolution — locked:**

| Endpoint | Canonical path |
|---|---|
| Request OTP | `POST /v1/auth/otp` |
| Verify OTP | `POST /v1/auth/verify` |
| Refresh token | `POST /v1/auth/refresh` |
| Logout | `POST /v1/auth/logout` |

The OpenAPI spec must be updated. The existing `/v1/auth/request-otp` and `/v1/auth/verify-otp` paths are deprecated and should be removed from the contract.

---

### 1.3 Institution Route Naming — Canonical Resolution

**The conflict:** `02_openapi.yaml` defines `POST /v1/official-posts` for institution posting. The dashboard pack introduces `POST /v1/institution/official-updates`. The governance spec also uses "official update" as the preferred term. The schema uses `official_posts` as the table name.

**Resolution — locked:**

The database table remains `official_posts` (this is an internal concern). The API route family and response terminology uses `official-updates` throughout, consistent with the dashboard pack. The table name does not need to change.

| Old path (deprecated) | New canonical path |
|---|---|
| `POST /v1/official-posts` | `POST /v1/institution/official-updates` |
| `GET /v1/admin/orphaned-signals` | `GET /v1/ops/orphaned-signals` |
| `GET /v1/admin/*` | `GET /v1/ops/*` (Hali ops routes) |

All admin routes from the original OpenAPI spec are replaced by the `/v1/ops/*` namespace. Do not maintain both.

---

### 1.4 Signal Submit Route — Canonical Resolution

**The conflict:** `02_openapi.yaml` defines `POST /v1/signals` for submit. `claude.md`, `openapi_patch_checklist.md`, and `mobile_screen_inventory.md` all expect `POST /v1/signals/submit`.

**Resolution — locked:**

`POST /v1/signals/submit` is canonical. `POST /v1/signals` is deprecated. Rationale: the preview/submit split needs distinct paths so mobile clients can hold preview state before committing.

---

### 1.5 Civic Category Enum — Canonical Resolution

**The conflict:** Four sources define the category list differently:

- `01_postgres_schema.sql` enum: `roads, water, electricity, transport, safety, environment, governance, health, education, other`
- `schema_patch_notes.md` expected: `roads, transport, electricity, water, environment, safety, governance, infrastructure` (removes health/education/other, adds infrastructure)
- `nlp_extraction_prompt.md` allowed: matches schema_patch_notes (8 categories)
- `seed_taxonomy_expanded.sql`: matches NLP prompt (8 categories, no health/education/other)

**Resolution — locked:**

The canonical MVP category set is the 8-category list from `schema_patch_notes.md` and the NLP prompt:

```
roads | transport | electricity | water | environment | safety | governance | infrastructure
```

`health`, `education`, and `other` are **removed** from the MVP enum. They are valid future categories and should be documented as planned future scope but must not be in the active DDL. The schema enum must be patched.

The OpenAPI `category` field throughout must use this 8-value enum explicitly — not `type: string` with no constraint as currently written in `02_openapi.yaml`.

---

### 1.6 seed_taxonomy_expanded.sql — Table Reference Bug

**The conflict:** `seed_taxonomy_expanded.sql` inserts into `civic_subcategories` which does not exist. The actual table is `taxonomy_categories`.

**Resolution — locked:**

`seed_taxonomy_expanded.sql` must be rewritten to insert into `taxonomy_categories(category, subcategory_slug, display_name)`. The column mapping is: `code` → `subcategory_slug`. The `description` column may be left null for the expanded entries. This seed file must not be applied until patched.

---

### 1.7 JWT Audience — Multi-Surface Resolution

**The conflict:** `.env.example` sets `JWT_AUDIENCE=hali-mobile`. Three web surfaces now exist alongside the mobile app.

**Resolution — locked:**

A single audience value is not appropriate across four surfaces. Use a platform-level audience:

```
JWT_AUDIENCE=hali-platform
```

Surface identity is established by the authenticated role and account type, not by JWT audience. The issuer (`JWT_ISSUER=hali-api`) and signing key remain shared across surfaces. Individual surface access is enforced by server-side role/scope checks, not by JWT audience discrimination.

---

## Part 2 — Schema Gaps

The following tables and columns are missing from `01_postgres_schema.sql`. Each must be added via EF Core migration, not by editing the reference SQL file directly (per the migration rules in `claude.md`). The SQL below is DDL reference for migration authors.

### 2.1 refresh_tokens (Critical — Phase 1)

Referenced in `claude.md`, `mvp_locked_decisions.md`, and `schema_patch_notes.md` but absent from the schema.

```sql
create table if not exists refresh_tokens (
    id uuid primary key default gen_random_uuid(),
    token_hash varchar(128) not null,
    account_id uuid not null references accounts(id) on delete cascade,
    device_id uuid references devices(id) on delete set null,
    created_at timestamptz not null default now(),
    expires_at timestamptz not null,
    revoked_at timestamptz,
    replaced_by_token_hash varchar(128),
    unique(token_hash)
);
create index if not exists ix_refresh_tokens_account_id on refresh_tokens(account_id);
create index if not exists ix_refresh_tokens_device_id on refresh_tokens(device_id);
create index if not exists ix_refresh_tokens_expires_at on refresh_tokens(expires_at);
```

The `replaced_by_token_hash` column supports token rotation: when a refresh token is exchanged, the old token is marked with the hash of its replacement, enabling anomaly detection (if an old already-rotated token is presented, it signals token theft).

---

### 2.2 institution_memberships (Phase 2)

The `institutions` table exists but there is no binding between institution users and institutions. Multiple spec docs reference this structure.

```sql
create table if not exists institution_memberships (
    id uuid primary key default gen_random_uuid(),
    institution_id uuid not null references institutions(id) on delete cascade,
    account_id uuid not null references accounts(id) on delete cascade,
    role varchar(60) not null,  -- institution_viewer | institution_operator | institution_manager | institution_admin
    is_active boolean not null default true,
    invited_by_account_id uuid references accounts(id) on delete set null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    unique(institution_id, account_id)
);
create index if not exists ix_institution_memberships_account on institution_memberships(account_id);
```

**Multi-institution doctrine (locked):** A single account may hold membership in at most one institution in Phase 2 MVP. The unique constraint above enforces this. Multi-institution membership is valid future scope but must not be implemented now.

---

### 2.3 institution_user_scopes (Phase 2)

Per-user geo and category scope assignments are needed for fine-grained permission. The existing `institution_jurisdictions` is per-institution, not per-user.

```sql
create table if not exists institution_user_scopes (
    id uuid primary key default gen_random_uuid(),
    membership_id uuid not null references institution_memberships(id) on delete cascade,
    scope_type varchar(30) not null,  -- 'geo' | 'category'
    locality_id uuid references localities(id) on delete cascade,
    corridor_name varchar(160),
    category civic_category,
    created_at timestamptz not null default now()
);
```

**Default behavior:** If no user-level scope rows exist for a membership, the user inherits the institution's full jurisdiction (`institution_jurisdictions`). Explicit user-scope rows act as restrictions within that institution jurisdiction — they cannot grant access beyond what the institution itself holds.

---

### 2.4 official_update_templates (Phase 2)

The Institution Admin Dashboard has template management. No table exists for this.

```sql
create table if not exists official_update_templates (
    id uuid primary key default gen_random_uuid(),
    institution_id uuid not null references institutions(id) on delete cascade,
    official_post_type official_post_type not null,
    category civic_category,
    name varchar(120) not null,
    title_template varchar(220),
    body_template text,
    is_active boolean not null default true,
    created_by_account_id uuid references accounts(id) on delete set null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);
```

---

### 2.5 institution_notification_recipients (Phase 2)

Institution Admin Dashboard has recipient management for institution-side alerts.

```sql
create table if not exists institution_notification_recipients (
    id uuid primary key default gen_random_uuid(),
    institution_id uuid not null references institutions(id) on delete cascade,
    account_id uuid references accounts(id) on delete set null,
    email varchar(255),
    notification_type varchar(80) not null,  -- 'new_cluster_in_scope' | 'cluster_activated' | 'restoration_triggered'
    is_active boolean not null default true,
    created_at timestamptz not null default now(),
    unique(institution_id, account_id, notification_type)
);
```

---

### 2.6 tda_snapshots (Phase 1, deferred build)

The TDA (Temporal Distribution Aggregator) is specified in `Hali_Signal_Lifecycle_TDA_Hardening_Spec.docx` but has no backing table. The analytics rollup worker needs somewhere to write pattern state.

```sql
create table if not exists tda_snapshots (
    id uuid primary key default gen_random_uuid(),
    cluster_lineage_id uuid not null,  -- links to the originating cluster's id for lineage tracking
    category civic_category not null,
    locality_id uuid references localities(id) on delete set null,
    spatial_cell_id varchar(80),
    temporal_class varchar(40) not null,  -- temporary | continuous | recurring | scheduled | episodic_unknown
    peak_hour_of_day smallint,
    peak_day_of_week smallint,
    recurrence_confidence numeric(5,4),
    pattern_data jsonb not null default '{}'::jsonb,
    snapshot_at timestamptz not null default now(),
    created_at timestamptz not null default now()
);
create index if not exists ix_tda_snapshots_locality_category on tda_snapshots(locality_id, category);
create index if not exists ix_tda_snapshots_cell_category on tda_snapshots(spatial_cell_id, category);
```

---

### 2.7 audit_log hardening (Phase 2, before ops dashboard)

The existing `admin_audit_logs` is too minimal for the governance spec requirements. The governance spec defines a richer model with before/after snapshots and rule versioning. The existing table must be migrated to this shape:

```sql
-- Migration: extend admin_audit_logs
alter table admin_audit_logs
    add column if not exists actor_role varchar(60),
    add column if not exists before_snapshot jsonb,
    add column if not exists after_snapshot jsonb,
    add column if not exists reason_code varchar(80),
    add column if not exists rule_version varchar(30);
```

CIVIS parameter changes must always include `rule_version` and both `before_snapshot` and `after_snapshot`. The ops UI must not allow saving CIVIS settings without these being captured automatically.

---

### 2.8 official_posts: missing is_restoration_claim (Phase 1)

The data model spec (`Hali_02_Data_Model_Storage_Spec.docx`) lists `is_restoration_claim bool` in `official_updates`. The lifecycle worker uses this flag to trigger the `possible_restoration` state transition. It is absent from the schema.

```sql
alter table official_posts
    add column if not exists is_restoration_claim boolean not null default false;
```

---

## Part 3 — Constraint Bugs

### 3.1 participations unique constraint is broken

**Current constraint:**
```sql
unique(cluster_id, device_id, participation_type, idempotency_key)
```

**The bug:** `idempotency_key` is nullable. In PostgreSQL, `NULL != NULL` for unique index purposes, meaning multiple rows can exist for the same `(cluster_id, device_id, participation_type)` if `idempotency_key` is null in any of them. The doctrine says "one participation type per device per cluster." The constraint does not enforce this.

**Resolution — locked:**

Two constraints are needed:
1. A partial unique index enforcing one current active participation per device per cluster (deduplication gate).
2. The idempotency key uniqueness for replay protection.

```sql
-- Remove existing constraint first via migration
-- Then add:
create unique index if not exists ix_participations_device_cluster_type
    on participations(cluster_id, device_id, participation_type)
    where device_id is not null;

-- Idempotency replay guard (separate)
create unique index if not exists ix_participations_idempotency
    on participations(idempotency_key)
    where idempotency_key is not null;
```

Additionally, application-layer enforcement must ensure only one *current* participation state per device per cluster (later participation transitions semantically supersede earlier ones while both rows are retained for audit history). This is an application-layer rule, not a pure DB constraint, because we need the history.

---

### 3.2 signal_clusters unique constraint is risky

**Current constraint:**
```sql
unique(locality_id, category, spatial_cell_id, state, title)
```

**The problem:** Including `state` in the unique key means the same cluster (same location, category, title) can exist simultaneously in different states — for example, one `active` row and one `resolved` row with identical real-world identity. State transitions also cannot update-in-place cleanly because the old `(locality_id, category, spatial_cell_id, old_state, title)` is unique and the new state would just change the row without constraint violation. This is actually fine for updates, but it means nothing prevents INSERT of a duplicate-identity cluster in a different state.

**Resolution — locked:**

Remove `state` from the unique constraint. Use a separate partial index to prevent duplicate active clusters in the same cell:

```sql
-- Replace existing unique constraint via migration with:
create unique index if not exists ix_clusters_active_identity
    on signal_clusters(locality_id, category, spatial_cell_id)
    where state in ('unconfirmed', 'active', 'possible_restoration');
```

This allows multiple resolved/expired historical clusters for the same cell (civic memory) while preventing duplicate live clusters. Resolved and superseded clusters are historical records, not competing realities.

---

## Part 4 — Citizen UI Gaps

### 4.1 Offline behavior doctrine (locked)

No offline behavior is defined. This is not optional — mobile apps on Kenyan networks will encounter connectivity gaps frequently.

**Doctrine:**
- **Read paths:** The home feed, cluster detail, and official updates surfaces must cache their last successful response and display it with a "last updated X ago" timestamp when offline. No blank states.
- **Write paths:** Signal composer submit, participation actions, and restoration responses must queue locally when offline and retry silently when connectivity resumes. The user must see a clear "queued — will submit when connected" indicator, not a failure state.
- **Idempotency keys** are generated client-side before submit. This means they can be generated at queue time and reused on retry — which is exactly what they're designed for.
- **OTP flow:** Fails cleanly with "check your connection" if offline. No local queuing for auth.

---

### 4.2 Location permission denial (locked)

No flow is defined for when the user denies location permissions.

**Doctrine:** The app must not gate reporting behind location permission. If location is denied:
- The signal composer shows a manual location search input as the primary entry point.
- The home feed asks the user to select a ward from a list rather than auto-detecting.
- Ward following setup is surfaced prominently on first launch if location is denied.
- Location confidence in NLP extraction is set to 0.0 when no device coordinates are available; the user correction UI appears mandatory.

---

### 4.3 NLP low-confidence correction flow (locked)

The `nlp_extraction_prompt.md` defines confidence thresholds but the UI behavior is underspecified.

**Doctrine:**
- `location_confidence >= 0.80`: Location label pre-filled, user can tap to edit but no mandatory confirmation prompt.
- `location_confidence 0.50–0.79`: Location label shown with a yellow "confirm location" badge. User must tap confirm or edit before proceeding to Step 3.
- `location_confidence < 0.50`: Location field shown as empty with a search input. User must enter location before proceeding.
- Same threshold logic applies to `condition_confidence`, using the same three-tier gate.
- If **both** location and condition are low confidence, Step 2 shows both correction inputs before the user can proceed. This is the "correction step" — do not skip it.

---

### 4.4 Ward following max-5 enforcement (locked)

The max-5 rule is stated but server-side enforcement behavior is not specified.

**Doctrine:**
- Server enforces: `POST /v1/localities/followed` returns `422 policy_blocked` with code `max_followed_wards_reached` when a 6th ward is attempted.
- Client must check the count before showing the "follow" button and replace it with a "manage" affordance when at capacity.
- `PUT /v1/localities/followed` (bulk set) must reject any payload with more than 5 items with the same 422 code.

---

### 4.5 Push token registration timing (locked)

Not specified in the mobile screen inventory or auth flow.

**Doctrine:** Push token registration happens at first successful OTP verification, not at app launch. Sequence:
1. OTP verify succeeds → session established.
2. App immediately calls `POST /v1/devices/push-token` with the Expo push token.
3. If push permission has not been requested yet, request it before calling the endpoint. If the user denies, skip the endpoint call silently — do not block onboarding.
4. On subsequent app launches, if the stored token differs from the current Expo token (token rotation), re-register silently.

---

### 4.6 Deep link strategy for restoration prompts (locked)

Push notifications for restoration prompts must deep link to the restoration prompt screen. This is not specified.

**Doctrine:**
- Restoration push notification payload must include `clusterId` in the data field.
- Deep link scheme: `hali://clusters/{clusterId}/restoration`.
- Expo Router handles this via `app/(modals)/restoration/[clusterId].tsx` or equivalent.
- When the app is backgrounded and the notification is tapped, navigate to the restoration prompt as a modal over the current stack, not by replacing it.
- When the app is cold-launched from the notification, navigate to the restoration prompt after session bootstrap completes.

---

### 4.7 Empty state doctrine for new localities (locked)

No empty state behavior is defined for home feed in a ward with no signals.

**Doctrine:** The "calm state" referenced in the demo pitch must be implemented explicitly. When `GET /v1/home` returns no active clusters and no official updates:
- Display: **"Currently calm in [ward name]"** with the last-checked timestamp.
- Show any upcoming scheduled disruptions in the official updates section even in calm state — this is proactive civic information.
- Do not show a generic empty illustration. Show the locality name and timestamp. This is the civic weather metaphor made literal.

---

## Part 5 — Institution Dashboard Data Exchange Architecture

### 5.1 What institutions can and cannot see

This is the most security-critical boundary in the platform.

**Institutions CAN see (via `/v1/institution/*`):**
- Cluster state, category, subcategory, dominant condition, location label, centroid, first/last seen timestamps.
- Public confirmation count (aggregate, never per-user breakdown).
- Affected count and observing count (aggregate).
- Restoration state and progression.
- Their own official updates and their linkage to clusters.
- Cluster state transitions as events (when it became active, when restoration was triggered).

**Institutions CANNOT see:**
- Account IDs, device IDs, or any identifier tied to individual citizen contributions.
- CIVIS scores, WRAB, SDS, MACF values, or any internal trust metric.
- Reason codes from CIVIS decisions.
- Device integrity levels or device fingerprint data.
- Which specific citizens participated (no user-level breakdown of participation).
- Clusters outside their geo-scoped jurisdiction (server-side enforced at the query layer, not just filtered in response).

**Implementation note:** The institution-facing cluster response DTO must be a distinct class from the internal cluster projection. Do not expose the full `signal_clusters` table shape. Create `InstitutionClusterSummaryDto` and `InstitutionClusterDetailDto` with the above field restriction explicitly enforced.

---

### 5.2 Institution → Citizen data flow

When an institution publishes an official update:

```
Institution user POSTs /v1/institution/official-updates
  → official_posts row created (status: published)
  → official_post_scopes rows created
  → outbox_events row emitted (event: official_post.published)
  → official-post-projection worker picks up
  → materializes advisory view for relevant localities
  → home feed GET /v1/home includes update for citizens in scoped wards
  → notification worker fans out to opted-in citizens in those wards
```

**Lag expectation:** Citizen-visible within ~10 seconds under normal queue conditions. This is async — document this for institution users. The dashboard must show the official update as "published" immediately from the institution's perspective; the citizen propagation lag is a background concern.

When an institution marks an update as a restoration claim (`is_restoration_claim: true`):

```
official_post created with is_restoration_claim = true
  → lifecycle_transition_worker detects restoration claim for linked cluster_id
  → cluster transitions to possible_restoration state
  → affected cohort receives restoration prompt push notification
  → restoration votes accumulate
  → threshold met → cluster transitions to resolved
  → institution dashboard restoration queue item updated to "resolved"
```

---

### 5.3 Citizen → Institution data flow

When citizens submit signals that activate in an institution's jurisdiction:

```
Cluster transitions to active state
  → cluster_state_changed event emitted
  → institution_notification_recipients checked for this locality/category
  → notification_worker sends institutional alert to registered recipients
  → institution dashboard /v1/institution/clusters shows new active cluster
```

**Polling strategy for institution dashboard (locked):** The institution operations overview refreshes on a 60-second polling interval. There is no WebSocket or SSE in Phase 2. This is sufficient for operational review — institutions are not real-time dispatch centers in Phase 2. Individual cluster detail pages refresh on-demand (user interaction). The dashboard must display the `freshness_timestamp` (last poll time) clearly in the overview header.

---

### 5.4 Institution API contracts (route-level)

These are the minimum response shapes needed. Full OpenAPI formalization is a Phase 2 pre-work task.

**GET /v1/institution/overview**
```json
{
  "institutionId": "uuid",
  "institutionName": "Kenya Power",
  "scopeSummary": { "wardCount": 12, "categories": ["electricity"] },
  "activeClustersInScope": 4,
  "possibleRestorationPending": 2,
  "unacknowledgedClusters": 1,
  "recentOfficialUpdates": [...],
  "trendCards": [{ "category": "electricity", "activeLast24h": 7, "resolvedLast24h": 3 }],
  "freshnessTimestamp": "2026-04-03T14:30:00Z"
}
```

**GET /v1/institution/clusters**

Query params: `localityId`, `wardId`, `corridorName`, `category`, `state`, `restorationState`, `dateFrom`, `dateTo`, `postedByMe`, `cursor`, `limit`

Response: Paginated list of `InstitutionClusterSummaryDto`. Cursor-based pagination. Default limit 25.

**POST /v1/institution/official-updates**

Consumes: `OfficialUpdateCreateRequest` (same structure as existing `OfficialPostCreateRequest` with `isRestorationClaim` boolean added).

Returns: `OfficialUpdateDto` with `id`, `status`, `scopes`, `linkedClusterIds`, `createdAt`.

**GET /v1/institution/restoration**

Returns clusters in `possible_restoration` state within scope, with restoration vote progress: `{ restorationRatio, affectedVoteCount, totalAffectedCount, threshold }`.

---

## Part 6 — Hali Ops Dashboard Data Exchange Architecture

### 6.1 Ops-exclusive data visibility

The ops dashboard sees everything, subject to role-level restrictions within ops roles. The key surfaces that are ops-exclusive:

- **CIVIS internals:** WRAB, SDS, MACF per cluster. Reason codes from `civis_decisions`. Activation pass/fail history.
- **Integrity anomaly queue:** Burst flags, device concentration alerts, identity churn patterns.
- **Cross-institution view:** All official updates from all institutions, unfiltered. Orphaned signal patterns.
- **Audit log completeness:** Full `admin_audit_logs` including before/after snapshots and rule versions.
- **Taxonomy management:** Create/deprecate categories and subcategories. Any taxonomy change must write an audit entry.
- **CIVIS parameter management:** Read and write CIVIS constants. Any write must record old values, new values, rule version, and actor. This is the most auditable action in the platform.

### 6.2 Orphaned signal detection algorithm (locked)

"Orphaned" is defined as: a `signal_cluster` in `active` or `possible_restoration` state where no institution with an active `institution_jurisdiction` covering the cluster's `locality_id` (or overlapping `spatial_cell_id`) has a matching category in their verified, active jurisdiction.

```
Orphaned = active/possible_restoration clusters WHERE
    NOT EXISTS (
        SELECT 1 FROM institutions i
        JOIN institution_jurisdictions ij ON i.id = ij.institution_id
        WHERE i.is_verified = true
          AND (ij.locality_id = cluster.locality_id OR ST_Intersects(ij.geom, cluster.centroid))
          AND (category match OR institution covers all categories)
    )
```

The ops dashboard must surface these ranked by: recurrence history > participation count > category sensitivity order.

### 6.3 Ops API contracts (route-level)

**GET /v1/ops/overview**
```json
{
  "platformHealth": { "workerLag": {...}, "queueDepths": {...} },
  "topActiveLocalities": [...],
  "unresolvedClusterCount": 142,
  "orphanedClusterCount": 8,
  "integrityAnomalyCount": 3,
  "institutionActivitySummary": [...],
  "freshnessTimestamp": "..."
}
```

**GET /v1/ops/integrity**

Query params: `anomalyType`, `severity`, `localityId`, `dateFrom`, `dateTo`, `relatedInstitutionId`, `cursor`, `limit`

Returns: List of `CivisAnomalyReviewItemDto` with cluster context, anomaly type, evidence summary, and current review status.

**PATCH /v1/ops/settings/civis**

Body: `CivisParameterUpdateRequest` with category-scoped constants. Returns: `CivisParameterSnapshotDto` with `ruleVersion`, `updatedAt`, `updatedBy`. Automatically generates audit entry with before/after snapshot. Restricted to Super Admin role only.

**GET /v1/ops/clusters/{clusterId}/civis-history**

Returns: Full `civis_decisions` history for a cluster including WRAB, SDS, MACF values, reason codes, and activation pass/fail decisions.

---

## Part 7 — Auth Strategy for Web Surfaces

### 7.1 Auth mechanism for institution and ops users (locked)

**Decision:** Institution users and Hali ops users authenticate via **email magic link** (passwordless) + **TOTP-based 2FA** for elevated actions.

Rationale:
- SMS OTP (Africa's Talking) is appropriate for citizen mobile onboarding where phone number is the identity anchor.
- Institution and ops staff are email-based organizational users. Magic link is lower-friction than password while remaining secure.
- Mandatory 2FA (per security runbook) is most naturally implemented via TOTP (Google Authenticator / Authy) for web users.

**Auth flow for web users:**

```
1. User enters email on web login page
2. POST /v1/auth/otp with method: "magic_link", destination: "email@org.com"
3. Backend sends magic link to email (via email provider, not Africa's Talking)
4. User clicks link → GET /v1/auth/magic?token={token}
5. Backend validates token, checks 2FA enrollment
6. If 2FA enrolled: redirect to TOTP entry screen
7. POST /v1/auth/totp/verify with {challengeId, totpCode}
8. If 2FA not enrolled (first login): redirect to 2FA setup screen
9. Session established → access token + refresh token issued
```

**New routes required for Phase 2:**

| Route | Purpose |
|---|---|
| `GET /v1/auth/magic` | Magic link redemption |
| `POST /v1/auth/totp/setup` | Enroll TOTP device |
| `POST /v1/auth/totp/verify` | Verify TOTP code |

**Citizen mobile continues using SMS OTP unchanged.**

---

### 7.2 Token storage for web surfaces (locked)

**Decision:** Web surface tokens must be stored in **httpOnly, Secure, SameSite=Strict cookies** — not in localStorage or sessionStorage.

Rationale: localStorage is XSS-accessible. The dashboard surfaces handle sensitive operational data. HttpOnly cookies prevent JavaScript from reading the token, eliminating the XSS token theft vector.

**Implementation notes:**
- The API backend must set the `Set-Cookie` header for web surface auth responses when the `X-Surface: web` header is present in the request.
- Mobile clients continue receiving tokens in the JSON response body and storing in Expo SecureStore.
- The `/v1/auth/refresh` endpoint must support both modes: cookie-based refresh for web, body-based refresh for mobile. Surface discrimination is via the `X-Surface` request header.

**New env variables needed:**
```
WEB_SESSION_COOKIE_DOMAIN=.hali.app
WEB_SESSION_COOKIE_SECURE=true
```

---

## Part 8 — Monorepo Tooling (locked)

**Decision:** Use **pnpm workspaces** as the monorepo package manager with **Turborepo** as the build orchestrator.

Rationale:
- pnpm's strict module resolution prevents phantom dependencies, which is critical when sharing packages between Next.js and the .NET backend's contract types.
- Turborepo's remote caching and task pipeline model is well-suited to the four-app structure with shared packages.
- Both are well-supported in Next.js and Expo ecosystems.

**Monorepo root `turbo.json` pipelines:**
```json
{
  "pipeline": {
    "build": { "dependsOn": ["^build"], "outputs": [".next/**", "dist/**"] },
    "dev": { "cache": false, "persistent": true },
    "lint": { "outputs": [] },
    "test": { "outputs": [] }
  }
}
```

**Shared package cross-platform rule (locked):**
- `/packages/design-system`: Web-only (Tailwind + shadcn/ui). **Must not be imported by citizen-mobile.**
- `/packages/contracts`: Framework-agnostic TypeScript types only. Safe to import from all surfaces including mobile.
- `/packages/taxonomy`: Plain JSON/TypeScript constants. Safe for all surfaces.
- `/packages/config`: Plain TypeScript constants. Safe for all surfaces.

The `citizen-mobile` app gets its own design primitives via React Native / Expo's native component system. Do not attempt to bridge the web design system into the mobile app.

---

## Part 9 — Real-Time Strategy (locked)

**Decision for Phase 2:** Dashboard surfaces use **polling**. No WebSocket or SSE in Phase 2.

Polling intervals by surface and context:

| Surface | Context | Interval |
|---|---|---|
| Institution Ops Overview | Page active | 60 seconds |
| Institution Live Signals | Table active, user focused | 30 seconds |
| Restoration queue | Page active | 30 seconds |
| Hali Ops Overview | Page active | 60 seconds |
| Hali Integrity Review | Page active | 45 seconds |

Polling must pause when the browser tab is backgrounded (use the Page Visibility API). It must resume and immediately re-fetch when the tab becomes active again.

**Phase 3 consideration:** WebSocket or SSE may be introduced for the Hali Ops integrity anomaly queue if real-time alerting proves necessary during pilot operations. Evaluate after Phase 2 data.

---

## Part 10 — Pagination Strategy (locked)

The API contracts doc states cursor-based pagination but no OpenAPI parameters are defined.

**Standard pagination parameters (all list endpoints):**

| Parameter | Type | Description |
|---|---|---|
| `cursor` | string (opaque) | Cursor from previous response. Omit for first page. |
| `limit` | integer | Items per page. Default 25. Max 100. |

**Standard pagination response envelope:**

```json
{
  "items": [...],
  "pagination": {
    "nextCursor": "string or null",
    "hasMore": true,
    "totalCount": 142
  }
}
```

The cursor is an opaque base64-encoded value containing the sort field value and ID of the last item on the page. Do not expose internal offset or timestamp values directly. `totalCount` is a best-effort estimate for dashboard list headers — it may lag by up to 5 seconds on high-write tables.

---

## Part 11 — Additional Undiscovered Gaps

### 11.1 Official post editing rules (locked)

No editing rules are defined. The institution admin dashboard mentions "edit/cancel allowed per role and state."

**Doctrine:**

| Update type | Editable fields | Allowed states | Who can edit |
|---|---|---|---|
| live_update | body | published | operator, manager, admin |
| scheduled_disruption | title, body, starts_at, ends_at, scopes | draft, published (if starts_at in future) | operator, manager, admin |
| advisory_public_notice | body | published | manager, admin |

**Cancellation:** Any published update can be cancelled (status → `withdrawn`) by manager or admin. Withdrawn updates are removed from citizen home feed within one polling cycle. Withdrawn updates are never deleted — they remain in audit history.

**No editing after `is_restoration_claim = true` updates** — these are lifecycle-triggering events and must remain immutable once published.

---

### 11.2 Taxonomy change safety doctrine (locked)

What happens to existing signals when a subcategory is deprecated?

**Doctrine:**
- Subcategories can be marked `is_active = false` in `taxonomy_categories`. This hides them from signal composer dropdowns and NLP taxonomy injection.
- Existing `signal_events` and `signal_clusters` retain their `subcategory_slug` values — no backfill. Historical accuracy is preserved.
- The ops dashboard taxonomy manager must warn before deactivating a subcategory with active clusters.
- Categories (top-level) cannot be deactivated while any active or possible_restoration cluster holds that category.

---

### 11.3 Institution cluster notification gap

No mechanism exists to notify institution users when a new cluster activates in their jurisdiction. The `institution_notification_recipients` table (Part 2.5) enables this, but the worker pipeline must explicitly handle it.

**Required pipeline addition:**
```
cluster_state_changed (state → active)
  → notification_worker
  → query institution_notification_recipients for locality/category match
  → send institutional email/push alert to matched recipients
```

This is distinct from citizen push notifications and must be handled as a separate notification_type: `cluster_activated_in_scope`.

---

### 11.4 raw_confirmation_count privacy ambiguity

`signal_clusters.raw_confirmation_count` — the word "raw" implies internal use but the column is on the main cluster table.

**Doctrine (locked):** `raw_confirmation_count` is an internal operational counter. It must not be included in any public-facing citizen or institution API response. The public-facing field is `public_confirmation_count` (from the data model spec). The cluster detail endpoint must use a derived DTO that maps `raw_confirmation_count` → omitted, and returns only the appropriate aggregate count for the surface.

---

### 11.5 Cluster state transition on signal_clusters UPDATE

When `state` changes on a `signal_clusters` row, there is currently no mechanism that guarantees an `outbox_events` row is emitted atomically. The lifecycle worker updates state directly. If the worker writes the state change and crashes before emitting the outbox event, the downstream pipeline never fires.

**Resolution (locked):** Use a PostgreSQL trigger on `signal_clusters` to automatically insert into `outbox_events` on `UPDATE` when `state` has changed. This makes the outbox emission transactional with the state change, eliminating the race.

```sql
create or replace function fn_emit_cluster_state_changed()
returns trigger as $$
begin
    if old.state <> new.state then
        insert into outbox_events(aggregate_type, aggregate_id, event_type, payload)
        values ('signal_cluster', new.id, 'cluster.state_changed',
                jsonb_build_object('old_state', old.state, 'new_state', new.state, 'occurred_at', now()));
    end if;
    return new;
end;
$$ language plpgsql;

create trigger trg_cluster_state_changed
after update on signal_clusters
for each row execute function fn_emit_cluster_state_changed();
```

---

## Part 12 — OpenAPI v3 Patch Summary

The following is the complete list of changes needed to `02_openapi.yaml` before Phase 1 ships and before Phase 2 begins.

**Phase 1 patches (required before mobile launch):**

1. Rename `/v1/auth/request-otp` → `/v1/auth/otp`
2. Rename `/v1/auth/verify-otp` → `/v1/auth/verify`
3. Add `POST /v1/auth/refresh`
4. Add `POST /v1/auth/logout`
5. Rename `POST /v1/signals` → `POST /v1/signals/submit`
6. Add `GET /v1/home`
7. Add `GET /v1/clusters/{id}/context` (POST)
8. Add `POST /v1/clusters/{id}/restoration-response`
9. Add `PUT /v1/localities/followed`
10. Add `POST /v1/devices/push-token`
11. Add `PUT /v1/users/me/notification-settings`
12. Add `GET /v1/users/me`
13. Fix `civic_category` enum to: `roads | transport | electricity | water | environment | safety | governance | infrastructure`
14. Add explicit enum constraint to all `category` fields (currently `type: string` with no enum)
15. Add pagination query parameters (`cursor`, `limit`) to all list endpoints
16. Remove `/v1/admin/*` routes — deprecated, replaced in Phase 2–3

**Phase 2 additions (before institution surface build):**
- Full `/v1/institution/*` route family with request/response schemas
- Full `/v1/institution-admin/*` route family
- Auth magic link and TOTP routes

**Phase 3 additions:**
- Full `/v1/ops/*` route family

---

## Part 13 — Implementation Priority Order

### Phase 1 (complete before any Phase 2 work)

1. Apply schema patches: `refresh_tokens`, `civic_category` enum fix, `is_restoration_claim`, trigger on `signal_clusters`
2. Fix `participations` unique constraint
3. Fix `signal_clusters` unique constraint
4. Patch OpenAPI spec (Phase 1 changes above)
5. Rewrite `seed_taxonomy_expanded.sql` to target `taxonomy_categories`
6. Replace placeholder `your-github-username` in four files (noted in build pack)
7. Implement offline queue for mobile write paths
8. Implement location permission denial flow
9. Implement ward max-5 server-side enforcement
10. Lock push token registration timing to post-OTP-verify

### Phase 2 (institution surfaces)

1. Schema: `institution_memberships`, `institution_user_scopes`, `official_update_templates`, `institution_notification_recipients`
2. Extend `admin_audit_logs` with new columns
3. Auth: magic link + TOTP flow and web cookie token strategy
4. Route families: `/v1/institution/*` and `/v1/institution-admin/*`
5. Institution DTO layer (separate from internal/citizen DTOs)
6. Institution notification pipeline for `cluster_activated_in_scope`
7. Monorepo setup: pnpm + Turborepo, shared packages
8. Institution Ops Dashboard (Next.js)
9. Institution Admin Dashboard (Next.js)

### Phase 3 (Hali Ops)

1. Schema: `tda_snapshots`, audit log full hardening
2. Route family: `/v1/ops/*`
3. CIVIS parameter management with versioned audit
4. Integrity anomaly queue surface
5. Orphaned signal detection implementation
6. Hali Ops Admin Dashboard (Next.js)

---

*End of reconciliation document. This document supersedes conflicting guidance in earlier pack files where noted. All items marked "locked" are implementation decisions — do not revisit during the relevant phase without explicit architectural review.*
