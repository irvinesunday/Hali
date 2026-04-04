# Hali Institution Dashboard — Canonical MVP UI/UX & Architecture Spec
**Version:** Freeze Edition v1.1  
**Status:** Locked for MVP prototype and engineering alignment  
**Scope:** Institution dashboard only (Phase 2)  
**Purpose:** Consolidated canonical specification for the Hali institution dashboard, merging original architecture doctrine with lessons validated through v0 MVP prototyping.

> **Changelog v1.0 → v1.1:**  
> - Added Phase placement and gate conditions (§0)  
> - Added Auth model for institution users (§3.5)  
> - Added `institution_response_stage` schema requirement (§11.7)  
> - Fixed §4.1 to mark institution list as seed data, not architecture  
> - Added trend computation basis to §9.3  
> - Added API dependencies per page (§8.7, §9.7, §10.9, §13.6, §14.6)  
> - Corrected §12 notification bell architecture (in-app alerts, not push)  
> - Added search endpoint note (§15.4)  
> - Added jurisdiction enforcement reference (§4.3)  
> - Marked "Avg First Response" as a derived metric (§14.3)  
> - Consolidated §11.4 / §11.5 preview overlap  
> - Clarified demo mode production boundary (§16.1)

---

## 0. Phase Placement

The institution dashboard is **Phase 2** in the Hali three-phase development structure:

| Phase | Scope |
|---|---|
| Phase 1 | Citizen mobile app (React Native + Expo) |
| **Phase 2** | **Institution dashboard (this document)** |
| Phase 3 | Hali Ops dashboard |

**Hard gate condition:** Phase 2 work must not begin until the Phase 1 citizen mobile app and the core backend (auth, signal intake, clustering, CIVIS, official updates, restoration lifecycle) are stable and passing CI.

The institution dashboard is a **web application** (desktop-primary). It is not part of the React Native app.

Technology decision: the institution dashboard is a web surface. Implementation may use React / Next.js or any framework consistent with the monorepo conventions in `Hali_05_Repository_Engineering_Conventions_Claude_Seed.docx`. The frontend is separate from the citizen app and the admin portal.

---

## 1. Purpose of This Document

This file is the canonical reference for the **Hali institution dashboard MVP**.

It defines:

- Product doctrine for institutional users
- Information architecture
- Page-level behavior
- Response model
- Filter and scoping rules
- Metrics and intelligence surfaces
- Real-time simulation behavior for demo mode
- Visual and UI conventions validated through v0

Claude, engineers, designers, and future product contributors should treat this file as the source of truth when implementing or modifying the Hali institution dashboard.

This document is intentionally focused on the **institution dashboard only**.  
Citizen mobile app architecture is specified separately in `mobile_screen_inventory.md`.

---

## 2. Product Identity

### 2.1 What the Institution Dashboard Is

The Hali institution dashboard is an **operational response surface** for public institutions.

It is not:

- A PR console
- A sentiment dashboard
- A case-management bureaucracy layer
- A social moderation tool

It is:

- A real-time operational visibility dashboard
- A scoped institutional response console
- A structured interface for seeing citizen signals, understanding where activity is concentrated, and posting progressive public updates
- A parallel official layer that coexists with citizen reality rather than overwriting it

### 2.2 Civic Weather Model

The institution dashboard inherits the Hali civic weather model:

- Citizen signals = lived conditions
- Official updates = advisories / action states
- Time = progression
- Resolution = convergence between official action and citizen confirmation

The dashboard does not erase citizen signals.  
It adds official state in parallel.

### 2.3 Core Loop (Invariant)

Every institution-side feature must support:

**Signal → Visibility → Response → Resolution → Confirmation**

If a proposed feature does not improve this loop, it should be rejected.

---

## 3. Core Doctrine

### 3.1 Institutional Neutrality

The dashboard must support institutions in responding visibly, but it must not distort the public truth.

It must not:

- Hide citizen signals
- Override lived experience with official declarations
- Collapse citizen feedback into internal-only process
- Present institutional statements as final truth before public confirmation

It must:

- Allow institutions to respond
- Show the response progressively
- Preserve prior update history
- Preserve citizen signal visibility

### 3.2 Action-Driven Communication

Institutional interaction must be **action-based**, not form-heavy.

The dashboard should let an operator act in one click wherever possible.

Examples:
- Teams on site
- Restoration in progress
- Service restored
- Post update

Avoid workflow patterns that feel bureaucratic or require too many steps to communicate a simple operational change.

### 3.3 Timeline-Based Legibility

Institutional communication must be chronological and readable.

Operators and the public should both be able to understand:

- what has happened
- what is happening now
- what changed recently
- whether restoration is underway

### 3.4 Operational Value Must Be Obvious

The dashboard should feel useful instantly.

A public institution leader or operator should look at it and immediately understand:

- how many relevant signals exist
- which areas need attention
- where reports are increasing
- what has already been posted
- what may be stabilizing

### 3.5 Auth Model for Institution Users

Institution dashboard access requires an institution user account (`account_type = institution_user`).

Rules:

- Institution users must complete **mandatory 2FA** before accessing the dashboard.
- Institution users are bound to a specific institution at account creation time via the `institution_memberships` relationship.
- Institution users may only see signals, clusters, and metrics within their institution's authorized jurisdiction.
- Jurisdiction scope is enforced server-side. The frontend should not present out-of-scope actions or data, but the API enforces it regardless.
- JWT tokens issued to institution users carry institution scope claims; the API rejects requests where the token scope does not match the requested institution resource.
- Admin users (account_type = admin) may access institution dashboard views for operational review but must not impersonate institution actors for response posting.

Auth flow:

1. Institution user logs in via email magic link or email OTP.
2. 2FA challenge (TOTP or equivalent) must be satisfied before dashboard access is granted.
3. Access token issued on success. Refresh token rotation applies per the locked auth strategy.

---

## 4. Institutional Scoping Model

### 4.1 Institution Selector — Seed Data

> **Architecture note:** The institution selector is not a hardcoded UI enum. Institutions are managed via the admin portal (Phase 3) and stored in the `institutions` table. The list below represents **Phase 2 MVP seed data** only — pre-seeded records used for prototype and demo purposes.

MVP seed institutions:

- Kenya Power
- Nairobi Water
- KenHA / KURA
- Nairobi County (where relevant for broad civic operations)

When an institution user logs in, the selector should default to their bound institution. Super Admin or Operations Admin roles accessing the dashboard may switch between institutions.

### 4.2 Automatic Scope Mapping

Each institution record carries an `institution_type` and a set of `institution_jurisdictions` rows defining its authorized scope.

The **Scope chip** should reflect the institution's category scope. The UI derives this from the institution record, not from a hardcoded frontend mapping.

Reference mappings for seed data:

- Kenya Power → electricity
- Nairobi Water → water
- KenHA / KURA → roads, transport
- Nairobi County → multi-category

### 4.3 Scope Enforcement

An institution should only see signals relevant to its scope.

Examples:
- Kenya Power should not see waste or flooding signals as primary operational items
- Nairobi Water should not see potholes as core signals
- KenHA / KURA should not see water outages as active scope signals

Cross-category items may appear only where explicitly configured for broad operators like Nairobi County.

**Server-side enforcement:** Scope filtering is enforced by the backend via the `institution_jurisdictions` table and the institution's category scope. The API at `GET /v1/institutions/dashboard/clusters` accepts category and locality parameters; the server validates that the requesting institution has jurisdiction before returning results. The dashboard must not attempt to retrieve out-of-scope data — such requests will return `403 forbidden_scope`.

### 4.4 Area Filtering

The dashboard must support locality filtering via an area selector.

Examples:
- All Areas
- South B
- Nairobi West
- Industrial Area
- Upper Hill
- Kilimani
- Lavington

All overview metrics, live signals, area snapshot states, metrics cards, and live activity feed must update according to the selected area.

Area options are populated from the `localities` table scoped to the institution's jurisdiction.

---

## 5. Information Architecture

The institution dashboard MVP includes:

1. Overview
2. Live Signals
3. Areas
4. Metrics

Persistent shell includes:
- left sidebar navigation
- top institutional toolbar
- live refresh control
- search
- notification bell

---

## 6. Global Layout & Shell

### 6.1 Desktop-First Decision

The institution dashboard is primarily a **desktop experience**.

MVP decision:
- Primary: desktop / laptop
- Secondary: basic mobile fallback only
- No need to optimize for polished mobile operator use in MVP

### 6.2 Sidebar

Left sidebar should include:

- Hali mark / logo
- Overview
- Live Signals
- Areas
- Metrics

Footer:
- Institution Dashboard
- v1.0.0 (or equivalent)

Sidebar behavior:
- clear active selection state
- soft mint / teal highlight for selected nav item
- persistent across pages

### 6.3 Top Toolbar

Top toolbar should include, from left to right:

- Institution selector
- Scope chip
- Area filter
- Refresh live activity button
- Search field
- Notification bell with badge count

Toolbar must remain visually calm and uncluttered.

### 6.4 Visual Feel

The dashboard must look like a modern, clean operations console.

It should feel:
- calm
- serious
- readable
- trustworthy
- immediately useful

---

## 7. Visual Design System

### 7.1 Color Character

The institution dashboard inherits the Hali visual family but uses a slightly more operational tone than the citizen app.

Observed canonical palette from v0 prototype:

- Background: very light cool gray / off-white
- Cards: white with subtle border and soft shadow
- Accent mint / teal: used for brand, highlights, success-oriented actions
- Amber / ochre: used for elevated or in-progress activity
- Soft red / coral: used for active / urgent state chips
- Lavender / soft violet: used for official-response stage chips in some cases
- Muted gray text: for metadata and timestamps

### 7.2 Recommended Token Set (approximate)

Use these as design targets rather than exact enforcement:

- App background: `#F6F8F7` to `#F8FAF9`
- Card background: `#FFFFFF`
- Primary text: `#1D2329`
- Secondary text: `#66707A`
- Border: `#DCE4E1`
- Brand teal / mint: `#5BAE9D` to `#63B5A5`
- Success green: soft mint-green
- Warning amber: warm muted ochre
- Active chip red: soft coral / muted red
- Neutral chip gray: pale gray / slate

### 7.3 Typography

The prototype reads as a clean sans-serif system.  
Recommended implementation target:

- Inter, Geist, or a similar neutral modern UI sans-serif
- Bold for key page titles
- Semibold for card titles and major labels
- Regular for metadata and descriptions

Typographic feel should match the demo:
- crisp
- spacious
- non-decorative
- legible at a glance

### 7.4 Card Language

Cards should use:
- rounded corners
- soft shadows
- generous spacing
- compact but readable metadata rows

No harsh table-heavy enterprise design.

---

## 8. Overview Page

### 8.1 Purpose

The Overview page gives a high-level operational summary for the selected institution and area.

The heading should update dynamically.

Examples:
- Real-time operational status for Kenya Power
- Real-time operational status for Nairobi Water in South B
- Real-time operational status for KenHA / KURA

### 8.2 KPI Tiles

The four primary overview tiles are:

1. Active Signals
2. Growing Signals
3. Updates Posted Today
4. Stabilized Today

These tiles must be clickable.

### 8.3 Tile Behavior

Clicking a tile should navigate or filter into the Live Signals view.

Examples:
- Clicking Active Signals → Live Signals filtered to active signals
- Clicking Growing Signals → Live Signals filtered to growing trend
- Clicking Updates Posted Today → Live Signals filtered to signals with official updates
- Clicking Stabilized Today → Live Signals filtered to stabilized / resolved / restoration-adjacent signals

### 8.4 Live Activity Panel

The Overview page should include a **Live Activity** panel.

This is a chronological activity feed showing recent operational-relevant events.

Examples:
- Power outage in South B is gaining reports
- Official update posted for South B water issue
- Possible power restoration triggered for Industrial Area
- New power signal emerging in Lavington
- Power issue in Nairobi West showing improvement

Feed items should include:
- icon
- readable event text
- relative time

These items should be clickable and open the relevant signal detail.

The Live Activity feed is populated from `outbox_events` and `civis_decisions` projections scoped to the institution's jurisdiction and category scope.

### 8.5 Areas Snapshot Panel

The Overview page should include an **Areas Snapshot** card listing locality status within the current institution scope.

Each row should show:
- Area name
- Signal count
- State chip:
  - Calm
  - Elevated
  - Active
- Chevron / navigational affordance

Rows must be clickable and filter the dashboard to that area.

### 8.6 Scoping Integrity

Overview content must change when:
- institution selector changes
- area filter changes

No cross-scope contamination is allowed.

Bad example:
- Nairobi Water dashboard showing Kenya Power activity strings
- KenHA / KURA area snapshot showing waste collection as top scoped issue unless a broader county view is selected

### 8.7 API Dependencies — Overview Page

| Purpose | Endpoint |
|---|---|
| Overview KPI tiles and summary | `GET /v1/institutions/dashboard/summary` |
| Scoped cluster list for activity feed | `GET /v1/institutions/dashboard/clusters` |
| Area locality options | Derived from institution jurisdiction metadata |

Query parameters for summary endpoint: `institutionId`, `localityId` (optional area filter), `since` (time window for "today" metrics).

---

## 9. Live Signals Page

### 9.1 Purpose

Live Signals is the primary operational work surface.

This page is where operators inspect scoped live signals and open details.

### 9.2 Header

Header should update based on institution and area.

Examples:
- Kenya Power signals
- Kenya Power signals in South B
- Nairobi Water signals in All Areas

### 9.3 Filter Bar

Live Signals page should support:

- Trend filter
- Status filter
- Clear filters action
- Signal count text

#### Trend filter values
- All Trends
- Growing
- Stable
- Slowing
- Possible Restoration

**Trend computation basis:**  
Trend is a derived field computed from the trajectory of a cluster's `sds` (Signal Density Score) relative to WRAB over a recent rolling window. It is not stored as a static column; it is computed at query time or cached in the `civis_decisions` snapshot.

| Trend label | Derivation |
|---|---|
| Growing | SDS increasing materially over the last evaluation window |
| Stable | SDS relatively flat within a tolerance band |
| Slowing | SDS declining but cluster still active |
| Possible Restoration | Cluster state = `possible_restoration` |

The backend is responsible for computing and surfacing this derived field in `GET /v1/institutions/dashboard/clusters` response payloads. The dashboard should not compute trend client-side.

#### Status filter values
- All Statuses
- No official update yet
- Acknowledged
- Teams dispatched
- Teams on site
- Work ongoing
- Restoration in progress
- Service restored

> **Schema note:** These status labels correspond to `institution_response_stage` values on the `official_posts` table — see §11.7 for the required schema addition.

### 9.4 Filter Summary Strip

When tiles or other controls drive a filtered state, show a strip such as:

- Showing: Active signals

This helps the operator understand why the current list is filtered.

### 9.5 Signal Cards

Each signal card should include:

- Category chip
- State chip (maps to `signal_clusters.state`)
- Trend text (derived — see §9.3)
- Signal title
- Location
- Affected count
- Duration (computed from `first_seen_at` to now)
- Official state chip on the right where applicable (maps to the latest `institution_response_stage` for this cluster)

Examples of right-side official state chips:
- No official update yet
- Teams dispatched
- Work ongoing

Signal cards must be clickable and open the signal detail panel.

### 9.6 Ordering

Live signals should be ordered dynamically, favoring operational priority.

Preferred order:
1. Growing active signals
2. High-volume unresolved active signals
3. Stable signals with no response yet
4. Possible restoration signals
5. Slowing / lower urgency items

### 9.7 API Dependencies — Live Signals Page

| Purpose | Endpoint |
|---|---|
| Scoped signal cluster list | `GET /v1/institutions/dashboard/clusters` |
| Filters applied as query params | `?state=active&trend=growing&localityId=...` |

The response must include derived `trend` and latest `responseStage` per cluster.

---

## 10. Signal Detail Panel

### 10.1 Structure

Clicking a signal opens a right-side detail panel or overlay.

This panel must be the main workspace for institutional response.

### 10.2 Header

Signal detail header should include:
- title
- close button
- optional category / state / trend chips near the top

### 10.3 Metadata Row

Display:
- location
- affected count
- active duration

### 10.4 Citizen Signal Summary

Include a summary card with:
- citizen-facing description (from `signal_clusters.summary`)
- current condition (from `signal_clusters.dominant_condition_slug`)
- human-readable interpretation of what people are experiencing

This section should help institutions understand the lived condition without needing raw complaint-style text dumps.

### 10.5 Signal Trend Card

Display:
- current trend (derived — see §9.3)
- recent report movement
- total affected (from `signal_clusters.raw_confirmation_count`)

Example:
- Possible restoration
- 8 reports in the last hour
- 312 total affected

### 10.6 Official Updates Timeline

This is a critical section.

It must show prior official updates in chronological order.

Allowed timeline states (maps to `institution_response_stage` — see §11.7):
- Acknowledged
- Teams dispatched
- Teams on site
- Work ongoing
- Restoration in progress
- Service restored

Rules:
- history is preserved — earlier posts are never overwritten
- later updates do not overwrite earlier ones
- times are visible
- expected resolution appears inline where applicable (sourced from `official_posts.ends_at`)

### 10.7 Response Actions Section

This is the key action surface.

Buttons:
- Teams On Site
- Restoration in Progress
- Service Restored
- Post Update

These actions should be visible only where they make sense.

Example:
- if Teams On Site has already been posted, that action may disappear or become disabled
- if Service Restored is posted, the panel should shift to restoration/confirmation-aware logic

Action availability is determined by the current `institution_response_stage` on the latest official post for this cluster. The backend enforces progression logic; the UI should disable actions that are out of sequence.

### 10.8 Action Philosophy

The panel must prioritize quick operational action over heavy form-filling.

The operator should be able to:
- inspect
- act
- continue

without navigating away into complex admin workflows.

### 10.9 API Dependencies — Signal Detail Panel

| Purpose | Endpoint |
|---|---|
| Read cluster detail | `GET /v1/clusters/{clusterId}` |
| Post response action (auto-post) | `POST /v1/institutions/updates` with `responseStage` field |
| Post custom update | `POST /v1/institutions/updates` with `postType = live_update` |
| Post restoration claim | `POST /v1/institutions/updates/{id}/restore` |
| Link update to cluster | `POST /v1/institutions/updates/{id}/link-cluster` |

All POST mutations must include an `Idempotency-Key` header.

---

## 11. Official Response Model

### 11.1 No Stage Dropdown as Primary Model

Primary interaction must be action-driven.

A generic "Response Stage" dropdown must not be the main way of responding to a signal.

The timeline is driven by actions, not by selecting a bureaucratic internal stage value.

### 11.2 Allowed Response Actions

#### Teams On Site
Auto-posts an `official_post` of type `live_update` with:
- `responseStage = teams_on_site`
- auto-generated body: _"Our team is now on site assessing the situation."_

#### Restoration in Progress
Auto-posts an `official_post` of type `live_update` with:
- `responseStage = restoration_in_progress`
- auto-generated body: _"Work is ongoing to restore service."_

#### Service Restored
Triggers:
- `POST /v1/institutions/updates/{id}/restore`
- Sets `responseStage = service_restored` on the resulting `official_post`
- Initiates citizen-side possible restoration / restoration confirmation flow

#### Post Update
Allows custom additional context via the Post Update modal. Creates an `official_post` of type `live_update` with a user-provided body and optional `ends_at` for expected resolution. `responseStage` is set by the operator or inferred from context.

### 11.3 Timeline Progression

Progression should feel coherent:

`Acknowledged → Teams dispatched → Teams on site → Work ongoing → Restoration in progress → Service restored`

Not every signal must use every stage, but the timeline should feel progressive. Stages must not regress (e.g., posting `Teams dispatched` after `Service restored` is a policy violation and must be rejected by the API).

### 11.4 Post Update Modal

The modal should include:

- signal being responded to (read-only context block)
- message input
- expected resolution date picker
- expected resolution time picker (these two together populate `official_posts.ends_at`)
- affected area auto-filled from cluster scope
- citizen preview block — see §11.5

Buttons:
- Cancel
- Post Update

### 11.5 Preview in Hali App (Optional — MVP)

The modal may include a "Preview in Hali App" panel.

This preview shows how the official update will appear in the citizen app surface.

Rules:
- preview must scale properly
- content must not truncate
- modal must remain scrollable
- no clipped text at viewport edges

**MVP status:** This is an enhancement. It is desirable but not a blocking requirement for Phase 2 MVP. Mark as `nice-to-have` in the build backlog.

### 11.6 Expected Resolution Input

Expected resolution should use:
- date picker
- time picker

Not a plain free-text field.

These values map directly to `official_posts.ends_at` (ISO 8601 UTC). The API uses this field for advisory expiry scheduling (`ExpireOfficialPostsJob`).

### 11.7 Required Schema Addition — `institution_response_stage`

> **This is a blocking correction.** The spec's entire response model depends on a `responseStage` concept that does not exist in the current schema.

The `official_posts` table currently has no `response_stage` column. The `official_post_type` enum (`live_update`, `scheduled_disruption`, `advisory_public_notice`) is a post type, not a response stage.

**Required addition to the schema:**

```sql
DO $$ BEGIN
    CREATE TYPE institution_response_stage AS ENUM (
        'acknowledged',
        'teams_dispatched',
        'teams_on_site',
        'work_ongoing',
        'restoration_in_progress',
        'service_restored'
    );
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

ALTER TABLE official_posts
    ADD COLUMN IF NOT EXISTS response_stage institution_response_stage;
```

Rules:
- `response_stage` is nullable — it is only populated when an institution post is used as an operational response to a cluster.
- Scheduled disruptions and advisory/public notices typically have `response_stage = NULL`.
- Live updates issued as operational responses must carry a `response_stage` value.
- The API must validate stage progression — a post cannot regress to an earlier stage for the same cluster.
- The `GET /v1/institutions/dashboard/clusters` response should include a `latestResponseStage` derived field per cluster (the `response_stage` of the most recent linked `official_post`).

**Migration:** Add this as a new EF Core migration in `Hali.Modules.Advisories` (or the Institutions module, whichever owns the `official_posts` table).

---

## 12. Notification System

### 12.1 Purpose

The notification bell provides a compact **in-app operational alert stream** for the institution operator.

> **Architecture distinction:** These are **not** Expo push notifications. The citizen-facing push notification system (via Expo Push API) is for citizen devices and is entirely separate. Institution dashboard notifications are in-app, web-based, and delivered via polling or a lightweight server-sent events (SSE) stream within the web application session. They do not use the `notifications` table (which is citizen-scoped) — they are a separate operator alert mechanism.

For MVP: polling on a short interval (e.g., 30s) against an institution alerts endpoint is acceptable. SSE or WebSocket is an enhancement for Phase 3 or post-MVP.

### 12.2 Allowed Notification Types

Examples:
- New signal emerging in scope
- Signal gaining reports
- Signal showing improvement
- Signal may need response (no official update after N minutes)
- Official update posted (confirmation echo)
- Possible restoration triggered

### 12.3 Behavior

Notifications should:
- open as a dropdown panel
- be institution-scoped
- update dynamically in demo mode
- be clickable

Clicking a notification should open the related signal detail.

### 12.4 Badge Count

Badge count should reflect unacknowledged notification volume. Clicking the bell marks notifications as read.

### 12.5 API Dependency — Notifications

An institution alerts endpoint is needed. This is not currently specified in the institution API surface in `Hali_03_API_Contracts_Client_Interaction_Spec.docx`.

**Required addition to the institution API surface:**

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/v1/institutions/alerts` | Fetch recent scoped operational alerts for the institution |
| `POST` | `/v1/institutions/alerts/acknowledge` | Mark alerts as read |

For MVP demo mode, alerts may be derived from recent `outbox_events` scoped to the institution's categories and jurisdiction.

---

## 13. Areas Page

### 13.1 Purpose

The Areas page gives a locality-by-locality operational snapshot for the selected institution scope.

### 13.2 Area Cards

Each area card should include:

- area name
- status chip:
  - Calm
  - Elevated
  - Active
- active signal count
- top issue chip where applicable
- last updated time

### 13.3 Click Behavior

Area cards should be clickable and filter the institution dashboard to that locality.

### 13.4 Calm State

If an area has no current signals in scope:
- show Calm
- show 0 active signals
- no top issue required

### 13.5 Scoped Integrity

Area page content must also respect institution scoping.

Bad example:
- KenHA / KURA showing waste collection signal in Industrial Area as scoped operational content

### 13.6 API Dependencies — Areas Page

| Purpose | Endpoint |
|---|---|
| Area-level cluster summary | `GET /v1/institutions/dashboard/summary?groupBy=locality` |
| Individual area drill-down | Reuses `GET /v1/institutions/dashboard/clusters?localityId=...` |

---

## 14. Metrics Page

### 14.1 Purpose

The Metrics page gives a compact operational intelligence view for the selected institution and area.

### 14.2 Primary LCR Card

The top featured metric is:

**Loop Closure Rate (LCR)**

Definition:
> Percentage of signals that reach confirmed resolution

This should be presented prominently with:
- percentage
- delta or trend
- resolved / in-progress / unresolved breakdown
- horizontal distribution bar

### 14.3 Supporting Metrics Cards

Include:
- Active Signals Now
- Avg First Response
- Updates Posted Today
- Stabilized Today
- Busiest Area
- Top Category

> **Derivation note for "Avg First Response":** This is a **derived/computed metric**, not a stored column. It is computed from: `MIN(official_posts.created_at) - signal_clusters.activated_at` for clusters that have at least one official post linked via `official_post_scopes` or `related_cluster_id`. This computation must happen in the analytics pipeline (`ProjectMetricsJob`) and be surfaced via `GET /v1/institutions/dashboard/summary`. Do not attempt to compute this in the frontend.

### 14.4 Charts / Distribution Views

Include:
- Active Signals by Area
- Categories This Week

Visuals should remain simple and readable.  
Avoid overcomplicated BI-style charts.

### 14.5 Metric Scoping

All metrics must change according to:
- selected institution
- selected area

### 14.6 API Dependencies — Metrics Page

| Purpose | Endpoint |
|---|---|
| All metrics data | `GET /v1/institutions/dashboard/summary` |
| Chart data (signals by area, categories by week) | `GET /v1/institutions/dashboard/summary?view=charts&window=7d` |

---

## 15. Search

### 15.1 Purpose

Search is a quick signal lookup.

### 15.2 Scope

Search should operate within:
- current institution scope
- current area filter

unless explicitly designed otherwise.

### 15.3 Targets

Search can match:
- signal title
- area name
- category
- landmark / location label

### 15.4 Search Endpoint

> **Gap note:** There is no dedicated search endpoint in the current institution API surface. For MVP, search may be implemented as a filtered query against `GET /v1/institutions/dashboard/clusters?q=<search_term>` where the backend does a PostgreSQL full-text search (`tsvector`) over `signal_clusters.title`, `signal_clusters.summary`, and `location_label_id → location_labels.location_label`.

**Required addition to the institution API surface:**

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/v1/institutions/dashboard/clusters?q=<term>` | Full-text search within institution scope |

The `q` parameter should be optional on the existing clusters endpoint, not a separate route.

---

## 16. Real-Time Simulation Behavior (Demo Mode)

### 16.1 Goals and Production Boundary

> **Important boundary:** This section describes behavior for the **Phase 2 MVP demo prototype**. The institution dashboard MVP is built as a demo-first surface before the full production backend is connected. When the production backend is ready, the simulation layer must be replaced with real API calls. Engineers must keep demo simulation code clearly isolated (e.g., a `useDemoMode` flag or a mock service layer) so it can be surgically removed without touching production UI components.

The institution demo must feel:
- alive
- scoped
- operationally useful
- responsive to emerging signals

### 16.2 Required Simulation Behavior

Simulate:
- new scoped signals appearing
- signal counts increasing
- trend changes
- official updates being posted
- notification events arriving
- metrics shifting
- area snapshot states changing

### 16.3 Refresh Button

"Refresh live activity" should trigger a believable refresh of:
- live activity feed
- notifications
- some metric surfaces
- signal ordering / counts in demo context

### 16.4 Institutional Response Latency

Not all signals should receive immediate official response.

Some should:
- remain with "No official update yet"
- receive updates later
- move to possible restoration after operational actions

### 16.5 Dynamic Signal Evolution

Signals should:
- emerge
- gain reports
- stabilize
- enter possible restoration
- occasionally resolve

### 16.6 Scope Accuracy During Simulation

Simulation must respect institution scope.

Examples:
- Kenya Power demo should generate power-relevant live activity
- Nairobi Water demo should generate water-related live activity
- KenHA / KURA should generate roads / traffic signals

### 16.7 No Static Zombie Events

Live Activity, notifications, and area snapshot states must not feel frozen.  
Events should rotate and refresh over time.

---

## 17. Citizen–Institution Relationship Rules

### 17.1 Parallel Truth

Institutional updates exist in parallel to citizen signals.

They do not:
- erase the signal
- replace lived experience
- close issues automatically

### 17.2 Restoration Trigger

When institutions mark a service as restored:
- the citizen signal should enter `possible_restoration` state
- the citizen confirmation loop should begin
- final closure depends on the restoration confirmation threshold (≥60% of affected voters confirm, ≥2 votes), not a single institutional declaration

This is triggered via `POST /v1/institutions/updates/{id}/restore`. The backend handles the state transition to `possible_restoration` and initiates restoration polling.

### 17.3 Official Update Language

Official language should be:
- calm
- factual
- concise
- operational

Not:
- defensive
- promotional
- political

---

## 18. Responsive Behavior

### 18.1 MVP Decision

Primary experience is desktop.

Mobile view may exist as fallback but is not a priority for polish.

### 18.2 Desktop Requirements

Desktop must feel:
- spacious
- structured
- easy to scan
- executive-ready

### 18.3 Modal and Panel Requirements

All panels and modals must:
- scroll cleanly
- avoid clipped text
- maintain padding
- respect viewport height
- keep footer actions visible

---

## 19. Locked MVP Learnings from v0 Prototyping

The following are explicitly locked based on institution dashboard prototype learning:

1. Scope filtering by institution is essential and high-value.
2. Overview tiles should be clickable and drill into live signals.
3. Live Activity is a major value surface and should remain prominent.
4. Area Snapshot must be clickable and useful, not decorative.
5. Signal Detail panel is the primary operator work area.
6. Official update history should be timeline-based and preserved.
7. Action buttons are better than heavy response forms.
8. LCR adds strong strategic value and should remain on Metrics.
9. Notifications should open actual signals, not dead-end text lists.
10. The dashboard should feel useful instantly to an institutional viewer without explanation.

---

## 20. What Must Never Happen

- Wrong institution shown for the selected scope
- Water update appearing in Kenya Power scope
- Waste or unrelated categories leaking into scoped operator view
- Response model centered on bulky forms
- Timeline updates overwriting history
- Static notifications with no navigation target
- Area snapshot acting as a dead UI surface
- Metrics failing to update when institution or area changes
- Cross-scope live activity pollution
- Mobile distortions taking priority over desktop polish in MVP
- Institution operator bypassing 2FA to reach the dashboard
- Response stage regression (e.g., posting `Teams dispatched` after `Service restored`)
- Demo simulation code bleeding into production API call paths

---

## 21. MVP Success Criteria (Institution Dashboard)

The institution dashboard MVP is successful if:

1. A viewer instantly sees operational value
2. Institution scope filtering feels correct and trustworthy
3. Signal detail makes it easy to inspect and act
4. Official response feels progressive and legible
5. Overview, Live Signals, Areas, and Metrics feel connected
6. The dashboard feels alive within 60 seconds
7. The UI looks like a modern public operations console, not a clunky enterprise admin tool
8. Auth (including 2FA) works correctly and institution scope is enforced server-side

---

## 22. Final Instruction for Claude / Builders

When generating or modifying institution dashboard features, always ask:

**Does this improve operational clarity, response speed, trust, and the civic feedback loop?**

If not, reject it.

When forced to choose between:
- more workflow complexity
- more internal process mimicry
- more feature sprawl
- more operational legibility

Choose:
- operational legibility
- speed
- scoping integrity
- timeline clarity
- one-click response affordances

---

## 23. Status

This document is now the **canonical institution dashboard MVP specification** for Hali, incorporating:

- original architecture doctrine
- institutional workflow principles
- v0 prototype learnings
- demo simulation requirements
- visual and UI conventions
- interaction and scoping rules
- schema corrections (§11.7)
- API dependency tables per page
- auth model for institution users (§3.5)

Citizen mobile app architecture is specified separately in `mobile_screen_inventory.md`.

---

**END OF FILE**
