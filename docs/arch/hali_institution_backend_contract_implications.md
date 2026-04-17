# Hali Institution — Backend Contract Implications

**Phase:** 1.5. **Issue:** #235. **Scope:** institution operational
web (Phase 2, #195–#198).

This document surfaces every backend-contract implication the
institution UX + component patterns create, so the Phase 2 backend
PRs implement the right shapes from the start. Each data need maps
to an endpoint — existing (✓) or **proposed** (new contract).
Proposed endpoints are flagged for the Phase 2 implementation PRs.

**Cross-references:**
- `02_openapi.yaml` — authoritative route list
- `docs/arch/hali_institution_ux_layout_spec.md` — page inventory
- `docs/arch/hali_institution_component_patterns.md` — component data shapes
- `docs/arch/SECURITY_POSTURE.md` §2 — scope enforcement rules

---

## 1. Scope-and-auth foundation (applies to every endpoint below)

- Every institution endpoint requires a JWT with
  `role = "institution"` and `institution_id` claim set.
- Server-side enforcement filters every list query by the authenticated
  `institution_id`; cross-institution data access is forbidden.
- Area filtering (via the topbar area picker) is an additional server-
  side filter on top of institution scope — never a client-side
  trim.
- JWT audience is `hali-platform` per `CLAUDE.md`.

**Canonical error shape:** `ErrorResponse` envelope already in
OpenAPI, used for every 4xx/5xx.

---

## 2. Per-page data needs

### 2.1 Overview page

**Needs:**

- Scoped summary counts: active signals, growing signals, updates
  posted today, stabilised-today.
- Area status list: up to 6 areas with condition + active-signal
  count + top category.
- Activity feed: recent state-change items in scope.

**Consolidation decision (per Phase 1.5 guardrail — "prefer
consolidation"):** all three needs are served by a single endpoint
rather than splitting the activity feed into its own route. One
request, one cache key, one refresh button on the topbar.

**Endpoint:** **proposed:** `GET /v1/institution/overview` (new, #195).

**Response sketch:**

```json
{
  "summary": {
    "activeSignals": 12,
    "growingSignals": 3,
    "updatesPostedToday": 7,
    "stabilisedToday": 2
  },
  "areas": [
    {
      "id": "uuid",
      "name": "South B",
      "condition": "active",
      "activeSignals": 4,
      "topCategory": "electricity"
    }
  ],
  "activity": [
    {
      "id": "uuid",
      "type": "growing",
      "message": "Electricity issue in South B gaining reports",
      "timestamp": "2026-04-17T05:12:40Z",
      "signalId": "uuid"
    }
  ]
}
```

Condition values are lowercase `active` / `elevated` / `calm` to
match the canonical wire vocabulary (see §4). Activity feed is
capped server-side at 15 items; the topbar refresh button busts the
cache key to re-fetch.

### 2.2 Live Signals page

**Needs:**

- Paginated list of signals in institution scope, with optional
  filter by state, trend, and area.
- Each row: id, title, area id + name, category, condition, trend,
  response status, affected count, recent reports (last 24h),
  time active.

**Endpoint:** **proposed:** `GET /v1/institution/signals` (new, #195).

**Query parameters:**

- `areaId` (optional uuid)
- `state` (optional: `active` | `growing` | `needs_attention` |
  `restoration`)
- `cursor` (optional string — opaque)
- `limit` (optional int, default 20, max 100)

**Response sketch:**

```json
{
  "items": [
    {
      "id": "uuid",
      "title": "Power outage affecting South B",
      "area": { "id": "uuid", "name": "South B" },
      "category": "electricity",
      "condition": "active",
      "trend": "growing",
      "responseStatus": "teams_dispatched",
      "affectedCount": 42,
      "recentReports24h": 12,
      "timeActiveSeconds": 7200
    }
  ],
  "nextCursor": "opaque"
}
```

### 2.3 Signal Detail page

**Needs:**

- Full signal fields (list item fields + description + citizen
  summary + location).
- Every posted official update for this signal in chronological
  order (oldest first, rendered top→bottom in the timeline — the
  server guarantees the order; the client renders as-returned).
- Current restoration state (if applicable).

**Consolidation decision:** the existing
`GET /v1/clusters/{id}` endpoint (see `02_openapi.yaml`
`#/components/schemas/ClusterResponse`) already carries everything
needed — `officialPosts[]` for the chronological update timeline,
and `restorationRatio` / `restorationYesVotes` /
`restorationTotalVotes` for the restoration aggregates. A new
dedicated `/v1/institution/clusters/{id}/updates` endpoint would
duplicate the cluster-detail response; skip it in Phase 2. Any
additional fields (see §3) are added to this endpoint.

**Endpoint:** `GET /v1/clusters/{id}` (existing; expanded via the
additive field changes in §3).

### 2.4 Areas page

**Needs:**

- Full area list in institution scope with condition + active signals
  + top category + last-updated timestamp.

**Endpoint:** **proposed:** `GET /v1/institution/areas` (new, #195).

Shape matches the `areas` array in the overview response but without
the 6-row cap.

### 2.5 Metrics page

**Needs:** the exact tile set is **requires-decision** and will land
incrementally during Phase 2. The tiles must source from the same
OTLP / Prometheus pipeline documented in
`OBSERVABILITY_MODEL.md` §8; a separate "metrics API" is not required
— the institution web calls Grafana / the OTLP-backed read API.

**No new endpoints** in Phase 2 for Metrics. Phase 3 may add
`GET /v1/institution/metrics/*` if a server-mediated read path is
needed for the web app.

### 2.6 Notifications centre

**Needs:**

- Recent notifications in scope with unread count.
- Mark-all-read action.

**Endpoints:**

| Need | Endpoint | Status |
|---|---|---|
| List | **proposed:** `GET /v1/institution/notifications` | new (#195) |
| Mark-all-read | **proposed:** `POST /v1/institution/notifications/read` | new (#195) |

---

## 3. New fields on existing endpoints

### `GET /v1/clusters/{id}` (existing)

- Add `responseStatus` field (enum — see §4) as an additive
  nullable field on `ClusterResponse`.
- Preserve the existing restoration aggregate fields already
  exposed on `ClusterResponse` — `restorationRatio`,
  `restorationYesVotes`, `restorationTotalVotes`. The institution
  Signal Detail consumes these directly; no parallel `restoration`
  object is introduced. If a restoration window expiry is needed,
  add it as a single additive nullable field `restorationWindowExpiresAt`
  rather than a nested object.
- Use the existing `officialPosts[]` collection to carry the
  chronological update timeline for the cluster (oldest first,
  newest last). Clients render that order directly.

### `POST /v1/official-posts` (existing)

- Ensure the full canonical set of update kinds is supported:
  `live_update`, `scheduled_disruption`, `advisory_public_notice`.
- Validate the location selector fields already present on
  `OfficialPostCreateRequest` — `localityId` and/or
  `corridorName` — against the caller's scope server-side. Do
  NOT introduce a new `affectedAreas[]` field unless it is first
  proposed as a request-shape change and added to `02_openapi.yaml`.
- `relatedClusterId` + `isRestorationClaim` (already on the
  request) remain the canonical path for institution-authored
  restoration claims.
- **Proposed additive change:** a new optional `severity` field
  for `scheduled_disruption`. If approved alongside Phase 2
  implementation, update `OfficialPostCreateRequest` in
  `02_openapi.yaml` in the same PR.

These additions must keep the wire format backward-compatible —
new fields are additive and nullable when absent.

---

## 4. Canonical enum vocabularies (server decides)

To avoid the v0 taxonomy mismatch documented in
`docs/reference-ui/v0/phase-1.5-visual-audit.md` §8, Phase 2 backend enforces these
canonical lowercase snake_case wire values. The decision at the
review gate: **use the backend's 8 canonical `CivicCategory` values**
with an explicit mapping for the 6 v0 categories so implementation
does not have to re-derive it.

### `CivicCategory` (8 values, canonical)

`roads`, `transport`, `electricity`, `water`, `environment`,
`safety`, `governance`, `infrastructure`.

### v0 category → canonical mapping

The v0 institution artifact uses 6 demo categories. Map them into
the canonical 8 as follows when consuming the v0 artifacts as
reference:

| v0 category | Canonical `CivicCategory` | Notes |
|---|---|---|
| `Power` | `electricity` | direct rename |
| `Water` | `water` | direct |
| `Roads` | `roads` | direct |
| `Traffic` | `transport` | canonical term is broader and includes traffic |
| `Drainage` | `infrastructure` | drainage is infrastructure, not environment |
| `Waste` | `environment` | waste collection is the environment category |

The two canonical categories the v0 demo does not cover —
`safety` and `governance` — exist server-side but do not appear
on any v0 screen. Institution web must handle them as first-class
categories (e.g. a county-level institution may own `safety` in
addition to `infrastructure`) even though no v0 screen demonstrates
the treatment.

**Institution → category membership** (mapping from
`institution_memberships` → allowed `CivicCategory` values) lives
server-side in the institution service; the client never hardcodes
this mapping.

### `SignalCondition`

`active`, `elevated`, `calm`.

### `SignalTrend`

`growing`, `stable`, `slowing`, `possible_restoration`.

### `ResponseStatus`

`no_official_update_yet`, `acknowledged`, `teams_dispatched`,
`teams_on_site`, `work_ongoing`, `restoration_in_progress`,
`service_restored`.

### `UpdateKind`

`live_update`, `scheduled_disruption`, `advisory_public_notice`.

### `NotificationType`

`new_signal`, `growing`, `needs_attention`, `restoration`,
`update_posted`, `restored`.

---

## 5. Pagination + filter semantics

- List endpoints return `{ items, nextCursor }`.
- Cursor is opaque; never a compound of database primary keys leaked
  to the client.
- `nextCursor === null` means end-of-list.
- Filters are additive (AND across parameters).
- Max `limit = 100`; default `20`.

---

## 6. Cache + refresh semantics

- Overview counts: cache for 10 seconds in Redis keyed by
  `institution_id + area_filter`; client's topbar "Refresh activity"
  button busts the cache for that key.
- Live Signals list: no server-side cache — driven by the client
  cursor pagination.
- Signal Detail: cache 30 seconds server-side; every state-changing
  write (new update, participation, restoration claim) invalidates
  the cached entry in the same transaction.
- Activity feed: return the last 15 items; refresh via the topbar
  button.

---

## 7. Rate limiting

Per `SECURITY_POSTURE.md` §8.b — every institution write endpoint
ships with a rate limit or a documented reason for omission:

- `POST /v1/official-posts` → per-institution-user rate limit,
  including institution-authored restoration claims
  (`isRestorationClaim=true`); budget TBD in the Phase 2
  implementation PR. Note that
  `POST /v1/clusters/{id}/restoration-response` is a citizen
  participation endpoint and is rate-limited separately under its
  existing citizen rules — it is NOT an institution write path.
- `POST /v1/institution/notifications/read` → no rate limit (idempotent).

---

## 8. Observability hooks (required from #195 onwards)

Per `OBSERVABILITY_MODEL.md` §3:

- Every new endpoint emits `api_exceptions_total` with
  `error_code`, `error_category`, `status_code` tags on failure.
- Every new write endpoint emits a dedicated counter for write
  outcome (success / failure / rate_limited).
- Cluster-lifecycle transitions triggered by institution actions
  continue to emit `cluster.*` events.
- Institution-admin privileged actions (#196) emit audit rows per
  `SECURITY_POSTURE.md` §4.

---

## 9. Summary of new endpoints (Phase 2 plan — consolidated)

Per the Phase 1.5 review-gate guardrail "_do not expand backend
surface unnecessarily — prefer consolidation_", the plan drops from
seven new endpoints to **five**:

| Route | Method | Owner | Notes |
|---|---|---|---|
| `/v1/institution/overview` | GET | #195 | Returns summary counts, area statuses, and the activity feed in one response |
| `/v1/institution/signals` | GET | #195 | Paginated list with filters |
| `/v1/institution/areas` | GET | #195 | Full areas list in scope (not capped at 6) |
| `/v1/institution/notifications` | GET | #195 | Notification list |
| `/v1/institution/notifications/read` | POST | #195 | Mark-all-read |

Plus field additions on the existing `GET /v1/clusters/{id}` to
surface the updates timeline and restoration state inline (see §3).

The institution-admin surface (#196) adds its own routes under
`/v1/institution-admin/*` — covered by that PR's contract spec, not
this document.

All paths are scoped server-side; none expose an `institutionId`
path parameter (the JWT claim is the source of truth).

---

## 10. Review-gate decisions (now binding)

Open items from synthesis resolved at the Phase 1.5 review gate:

- **Endpoint consolidation** — preferred over expansion. The
  activity feed is returned inline in
  `/v1/institution/overview`; the updates timeline is returned
  inline in `GET /v1/clusters/{id}` (see §2.1, §2.3, §3, §9).
- **Restoration-claim** — reuse the existing official-post write
  path for institution-authored claims, setting
  `isRestorationClaim = true` and
  `relatedClusterId = <signal cluster id>` on the
  `OfficialPostCreateRequest`. No new endpoint.
  `POST /v1/clusters/{id}/restoration-response` remains the citizen
  participation vote endpoint (carries `deviceHash` +
  `still_affected | restored | not_sure`) and is not called by the
  institution surface.
- **Metrics** — no server-mediated read path in Phase 2. The
  institution Metrics page consumes Grafana / the existing OTLP
  pipeline directly. A dedicated route may land in Phase 3 if
  needed.
- **JWT-scoped institution resolution** — binding rule: no
  `institutionId` in URLs. The JWT `institution_id` claim is the
  single server-side scope anchor.

Remaining open (owned by the Phase 2 implementation work, not
this spec):

- Exact rate-limit budgets for the new write endpoints.
- Whether `/v1/institution/notifications` needs cursor pagination
  or can return a fixed latest-N list. Default: fixed latest-50,
  revisit if notification volume warrants.
