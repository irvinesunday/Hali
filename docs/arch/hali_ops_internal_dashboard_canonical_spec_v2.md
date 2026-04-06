# Hali Ops Internal Dashboard — Canonical Architecture Spec
**Version:** v2.0 (Reviewed & Reconciled)
**Status:** Locked for Phase 3 product architecture and engineering alignment
**Scope:** Hali internal operations dashboard only
**Phase:** Phase 3 (Hali Ops Dashboard) — see Phase Structure note below
**Replaces:** Freeze Edition v1.0

> **Changelog from v1.0:**
> 16 gaps and inconsistencies identified and resolved. Key changes: phase placement declared,
> duplicate cluster detail definitions unified, "orphaned signals" terminology corrected to
> "orphaned clusters" throughout, CIVIS threshold exposure removed from Settings to preserve
> doctrine, audit log schema aligned to Admin Governance Spec, LCR corrected to cluster level,
> outbox mandate added to operator actions, confidence band defined, NLP re-run action
> specified, demo mode added to Settings, TDA referenced in Locality Detail, system health
> added to Overview KPI row, admin role schema gap documented, Admin API surface added.

---

## Phase Structure Note

Hali is developed in three phases with hard gates:

- **Phase 1** — Citizen mobile app only
- **Phase 2** — Institution dashboards
- **Phase 3** — Hali Ops dashboard (this document)

The Hali Ops dashboard is a **Phase 3 deliverable**. Nothing in this spec should be built
during Phase 1 or Phase 2. References to "MVP" within this document mean the MVP of Phase 3.

Cross-reference points:
- Citizen mobile architecture: `mobile_screen_inventory.md`
- Institution dashboard architecture: separate canonical spec (Phase 2)
- Engineering conventions: `CLAUDE.md` and `Hali_05_Repository_Engineering_Conventions_Claude_Seed.docx`

---

## 1. Purpose of This Document

This file is the canonical reference for the **Hali Ops internal dashboard**.

It defines:

- the purpose and doctrine of Hali Ops
- the internal information architecture
- page structure and operational workflows
- trust and integrity review surfaces
- institution coverage and routing logic
- internal metrics and observability
- roles, permissions, and the admin role schema
- the required Admin API surface
- UI principles and visual behavior

Claude, engineers, designers, and future product contributors should treat this file as the
source of truth when implementing or modifying the Hali Ops dashboard.

This document covers the **internal Hali operations layer only**.
Citizen mobile app and institution dashboard architectures are maintained as separate specs.

---

## 2. Product Identity

### 2.1 What Hali Ops Is

Hali Ops is the **internal control center** for the Hali platform.

It is not:
- a public dashboard
- an institution-facing dashboard
- a complaints moderation desk
- a truth arbitration system
- a customer support inbox

It is:
- the internal operating console for the Hali system
- the meta-layer that monitors platform health and integrity
- the place where Hali's team monitors signals, routing, institutions, anomalies, and civic loop
  performance
- the system-wide control tower for Hali as civic infrastructure

### 2.2 Mental Model

If the citizen app is the **public civic weather interface**, and the institution dashboard is
the **response console**, then Hali Ops is the **air traffic control tower**.

It exists to answer:

1. Is the platform healthy?
2. Is the data trustworthy?
3. Are institutions correctly mapped and responding?
4. Where is Hali becoming useful?
5. What requires internal intervention?

### 2.3 Core Loop (Invariant)

All Hali Ops functionality must support:

**Signal → Visibility → Response → Resolution → Confirmation**

The internal dashboard must strengthen the loop, not complicate it.

---

## 3. Core Doctrine

### 3.1 Neutrality Preservation

Hali Ops must protect the neutrality doctrine of the wider system.

Internal operators must not:
- manually rewrite civic reality because it is inconvenient
- suppress citizen signals for reputational reasons
- decide whether a lived issue is "true" in a political sense
- convert Hali into a dispute-resolution tribunal

Internal operators may:
- monitor signal integrity
- review abuse or anomalies
- merge duplicate clusters
- improve routing
- correct platform failures
- identify coverage gaps
- inspect and improve system behavior

Hali Ops must preserve the principle that Hali is **weather, not court**.

### 3.2 Platform Stewardship

The dashboard exists to steward:
- signal health
- trust integrity
- locality coverage
- institutional mapping
- routing quality
- civic loop performance
- operational safety of the system

### 3.3 Internal Clarity Over Feature Sprawl

The Hali Ops dashboard must prioritize:
- internal clarity
- signal legibility
- reliable interventions
- auditability
- trustworthiness

It must not become a maze of operational knobs with unclear impact.

---

## 4. Primary Objectives

The dashboard must help Hali's internal team answer five questions quickly:

### 4.1 Platform Health
Is Hali functioning correctly right now?

### 4.2 Trustworthiness
Can we trust the signals and patterns we are seeing?

### 4.3 Coverage & Routing
Do active clusters have correct institutional owners, and where are the blind spots?

### 4.4 Growth & Utility
Where is Hali gaining useful civic density and producing closed loops?

### 4.5 Intervention Queue
What needs internal human review or action?

These five objectives shape the entire architecture.

---

## 5. Functional Architecture Layers

The Hali Ops dashboard is structured around five major internal layers:

### 5.1 System Health
Is the platform itself healthy and operational?

### 5.2 Signal Operations
What active clusters exist, and how are they evolving?

### 5.3 Trust & Integrity
What anomalies, abuse patterns, or suspicious activity require review?

### 5.4 Institution Coverage & Routing
Who owns which categories and localities, and where do orphaned clusters exist?

### 5.5 Metrics & Intelligence
Is the civic loop working, and where is Hali becoming meaningfully useful?

---

## 6. Information Architecture

Top-level navigation (canonical left sidebar order):

1. Overview
2. Live Signal Operations
3. Localities
4. Institutions
5. Integrity Review
6. Metrics
7. Team & Roles
8. Audit Logs
9. Settings

---

## 7. Global Layout & Shell

### 7.1 Experience Priority

Hali Ops is a **desktop-first internal console**. No full mobile optimization required for
Phase 3 MVP. Internal use should prioritize high-density, multi-panel desktop workflows.

### 7.2 Sidebar

The left sidebar includes:
- Hali mark / logo
- All nine navigation items from §6
- Sidebar footer: environment label (e.g. `MVP / staging / demo`), version number

### 7.3 Top Toolbar

The top toolbar includes:
- **Locality selector** — scoped to the Hali locality hierarchy: country → county → city → ward.
  Do not use loose "city / region" language in implementation; drive this from the `localities`
  table and the locality hierarchy.
- Time range selector
- Global search
- Alerts / integrity notifications badge
- Signed-in operator identity
- Demo mode indicator (visible badge when demo simulation is active; see §17 and §20)

### 7.4 UI Tone

The dashboard should feel calm, operational, modern, internal, trustworthy, and
oversight-oriented rather than flashy. Control tower, not admin spreadsheet.

---

## 8. Visual Design System

### 8.1 Design Character

Hali Ops should visually align with the citizen and institution dashboard family, while feeling
slightly more internal and analytical.

Preserve:
- soft modern cards
- breathable spacing
- rounded surfaces
- subtle shadows
- clean sans-serif typography
- muted but meaningful semantic color

### 8.2 Color Character

- background: very light cool gray / off-white
- cards: white
- primary text: deep charcoal
- secondary text: muted slate gray
- mint / teal: Hali operational accent
- amber: elevated caution
- coral / red: urgent anomaly / active risk
- lavender / blue: informational / system state
- soft green: resolved / stable / healthy

### 8.3 Typography

Inter, Geist, or equivalent modern neutral sans-serif.
- bold: major page headings
- semibold: card titles, important counters
- regular: metadata, secondary labels

Legibility over density.

### 8.4 Data Display Style

Prefer: cards, structured panels, readable lists, clear badges, compact trend markers.

Avoid: dense enterprise tables as the dominant pattern, overly technical system-monitor
styling, noisy color usage.

---

## 9. Overview Page

### 9.1 Purpose

Answers: **What is happening across Hali right now?**

This is the executive pulse of the system — both platform health and civic activity.

### 9.2 Primary KPI Row

**System Health group** (leftmost):
- Worker Queue Health — composite indicator: OK / Degraded / Alert
- NLP Pipeline Lag — current lag in seconds
- CIVIS Pipeline Lag — current lag in seconds

**Operational group:**
- Active Clusters
- New Signals (Last Hour)
- Possible Restorations
- Resolved Today
- Orphaned Active Clusters *(see terminology note in §9.5)*

**Integrity & Governance group:**
- Integrity Review Queue (open items)
- Institutions Active Today
- Loop Closure Rate (Today / 7d)

> **Why system health is in the Overview KPI row:** §5.1 declares System Health as the first
> functional layer. The overview must reflect all five layers, not only operational/mission
> metrics. Without health indicators here, an operator landing on the Overview page would miss
> a degraded pipeline.

### 9.3 Platform Pulse Feed

A live activity feed showing recent system-wide operational events, for example:
- Cluster activated in South B
- CIVIS flagged burst in Industrial Area
- New orphaned cluster: flooding in Umoja with no mapped institution
- Kenya Power posted update in Nairobi West
- Water restoration confirmed by affected users in Upper Hill

Each item includes:
- icon / event type marker
- event text
- locality
- relative time
- clickable navigation target

### 9.4 Locality Health Snapshot

Ranked locality overview:
- most active localities
- elevated localities
- calm localities
- localities with repeated unresolved clusters

Each row includes: locality name, current state (Calm / Elevated / Active), active cluster
count, dominant repeated category if any, response presence / absence indicator.

### 9.5 Coverage Risk Snapshot

> **Terminology note:** Throughout this document, "orphaned" refers to **clusters** (not raw
> signal events) that map to a civic category but have no onboarded institution owner. Raw
> signal events cannot be orphaned in the institutional sense. Use "orphaned cluster" or
> "orphaned active cluster" consistently. Do not use "orphaned signal."

Show:
- orphaned clusters by locality
- categories with no mapped institution
- localities with active clusters but no official engagement
- gaps in operational ownership

This surface identifies onboarding leads and routing risk.

### 9.6 Integrity Alerts Snapshot

Show:
- suspicious bursts
- low device diversity clusters
- repeated single-account patterns
- geo-inconsistent reports
- duplicate-cluster collision risk

Links directly into the Integrity Review queue.

---

## 10. Live Signal Operations Page

### 10.1 Purpose

Platform-wide operational queue for Hali's internal team. Oriented around **clusters**, not
raw individual signal events.

### 10.2 Default Content

Each cluster row / card shows:
- cluster title
- category
- locality / ward / road label
- cluster state
- trend
- **confidence band** *(defined in §10.6 below)*
- participation count
- affected vs observing split
- institutional owner status
- response status
- time active
- restoration status
- integrity flags if any

### 10.3 Filter Controls

Filters:
- locality / region
- category
- state
- trend
- confidence band
- institution mapped / unmapped
- official response present / absent
- orphaned clusters only
- integrity-flagged only
- recent only / aging only

### 10.4 Cluster Detail Panel

Clicking a cluster opens the Cluster Detail panel. This is the **canonical** cluster detail
definition for Hali Ops (§19 is removed as a duplicate; all references to cluster detail in
this document point here).

#### A. Header
- cluster title
- category and subcategory
- state (badge)
- location label (human-readable)
- active duration
- confidence band

#### B. Citizen Signal Summary
- affected count
- observing count
- first seen / last seen
- report velocity
- NLP-derived condition summary (neutral language)

#### C. Trust / CIVIS Summary
- confidence band and underlying reason codes
- device diversity status (pass / at-risk / fail)
- geo consistency status
- burst anomaly status
- relevant CIVIS reason codes (internal codes only; numeric scores never shown in UI)

#### D. Institution Routing
- current mapped institution (or "No institution mapped")
- ownership source (auto-mapped / manually assigned)
- routing history
- routing confidence level
- whether an institution user has engaged on this cluster

#### E. Official Updates Timeline
- visible timeline of official posts linked to this cluster
- whether a response exists
- latest update state

#### F. Operator Tools
- Merge duplicate cluster (opens merge confirmation with audit reason input)
- Remap institution owner (opens institution picker)
- Mark reviewed (clears from queue; creates audit entry)
- Tag as orphaned lead (flags for institutional outreach)
- Suppress (abuse/spam path; requires policy-coded reason and dual-step confirmation)
- Escalate for deeper review
- Inspect raw event timeline
- Re-run NLP suggestion (see §10.5)

> **Outbox mandate:** Every operator action in this panel that changes cluster state, routing,
> or suppression status must: (1) write to `admin_audit_logs`, and (2) emit an outbox event
> via the outbox pattern as mandated by `CLAUDE.md` engineering rules. Actions that only mark
> reviewed without changing state still write an audit entry but do not need to emit an
> outbox event.

### 10.5 Re-run NLP Suggestion

When an operator triggers "Re-run NLP suggestion" on a cluster:

1. The current cluster's `location_label`, `subcategory_slug`, and `condition_slug` are sent
   to the CSI-NLP pipeline with the original source text of the founding signal event.
2. CSI-NLP returns a new structured candidate.
3. The operator sees a diff: current values vs. suggested new values.
4. The operator may accept, reject, or partially accept the suggestion.
5. Any accepted change is applied as a cluster update, not a silent overwrite.
6. The action writes a `CISNLP_RERUN` entry to `admin_audit_logs` regardless of outcome.
7. This action does not create an outbox event unless the operator accepts changes that affect
   cluster state.

This action is available only to Operations Admin and Super Admin.

### 10.6 Confidence Band Definition

**Confidence band** is a human-readable presentation tier derived from the internal CIVIS
score. It must never expose the raw numeric `civis_score`, `SDS`, `MACF`, or threshold
parameter values in any UI surface (citizen, institution, or ops).

Suggested bands:

| Band label | Meaning |
|---|---|
| Low | Cluster is unconfirmed or has weak supporting evidence; at risk of not graduating |
| Moderate | Cluster is active but evidence base is thin; could decay if new signals stop |
| Strong | Cluster is well-supported; evidence is diverse and recent |
| Flagged | Cluster has an integrity anomaly under review |

Implementation: derive band from a combination of `civis_score`, device diversity status, and
anomaly flag — all computed server-side. The band label and its reasoning code are the only
values sent to the UI.

### 10.7 Operator Philosophy

This page is for active monitoring, system sanity, and internal intervention. It is not for
manually deciding public truth.

---

## 11. Localities Page

### 11.1 Purpose

Answers:
- where activity is concentrated
- where recurring issues exist
- where institutions are or aren't responding
- where Hali is becoming useful

### 11.2 Locality Card Structure

Each locality card / row shows:
- locality name (ward-level, drawn from the `localities` table)
- current state: Calm / Elevated / Active
- active cluster count
- dominant repeated category
- response presence indicator
- orphaned cluster count
- recent resolution count
- locality LCR if computed
- last significant update time

### 11.3 Locality Detail View

Clicking a locality opens a deeper view:
- active clusters (linked to Live Signal Operations)
- recurring categories — powered by TDA (Temporal Distribution Aggregator); see
  `Hali_Signal_Lifecycle_TDA_Hardening_Spec.docx` for the canonical definition of recurring
  pattern classes and TDA scoring
- official updates in that locality
- orphaned clusters within the locality
- institution coverage map / ownership view
- recent closure behavior
- signal density trend (TDA-derived)
- locality-level civic utility trend (LCR over time for this locality)

### 11.4 Purpose of This View

City-manager lens. Reveals density hubs, unresolved localities, emerging opportunity areas,
repeated civic pain zones.

---

## 12. Institutions Page

### 12.1 Purpose

Both an operational routing surface and an institutional coverage management view.

### 12.2 Subviews

#### A. Active Institutions

For each institution:
- institution name
- status (active / inactive / onboarding / trial)
- areas covered (expressed as localities — wards, corridors, or geo-polygons)
- categories owned
- active internal users
- latest official update
- average first response time
- loop closure contribution

#### B. Coverage Map / Ownership Matrix

- categories by locality grid
- institution ownership per cell
- ambiguous ownership gaps
- overlapping ownership cases

#### C. Orphaned Clusters / Gap Discovery

- active orphaned clusters (clusters with no institution mapped)
- repeated orphaned categories by locality
- localities with persistent owner gaps
- likely institution candidates to onboard

### 12.3 Strategic Importance

Orphaned clusters are not only ops issues. They are onboarding evidence, product-market-fit
signals, and institutional sales leads. This page serves both operations and growth.

---

## 13. Integrity Review Page

### 13.1 Purpose

Reviews trust, anomaly, and abuse patterns surfaced by CIVIS and internal systems. One of the
most strategically important internal pages.

### 13.2 Queue Item Types

- low device diversity
- temporal burst anomaly
- location inconsistency
- duplicate cluster collision risk
- repeated single-account submissions
- category / location mismatch
- abnormal volume spike
- suspicious graduation pattern
- election-mode integrity alerts (future phases)

### 13.3 Required Fields per Queue Item

| Field | Description |
|---|---|
| Reason code | CIVIS internal reason code |
| Severity | Low / Medium / High |
| Confidence band | As defined in §10.6 |
| Locality | Affected ward |
| Associated cluster | Linked cluster ID |
| Timestamp | When flagged |
| Recommended action | System-suggested next step |

### 13.4 Allowed Operator Actions

- Mark reviewed (clears item; writes audit entry)
- Merge duplicate clusters
- Suppress from public graduation (abuse/spam; policy-coded reason required; two-step
  confirmation required; outbox event emitted)
- Escalate for further review
- Remap category / location if NLP clearly failed (see §10.5)
- Inspect raw signal history
- Inspect device diversity summary

> **Outbox mandate:** Suppression actions must emit an outbox event in addition to writing to
> `admin_audit_logs`. All other review actions write audit entries but do not require an
> outbox event unless cluster state changes.

### 13.5 Important Limitation

The trust queue exists to review system integrity, not to let humans casually rewrite civic
reality. Operators must not be given broad "truth override" powers. Suppression requires a
policy-coded reason and dual-step confirmation at minimum, per Admin Governance Spec §7.

---

## 14. Metrics Page

### 14.1 Purpose

Internal intelligence layer for platform performance and civic utility. Product KPIs and
Mission KPIs must be visually and conceptually distinct.

### 14.2 Product KPIs (Platform Health)

Track platform behavior indicators:
- active contributors
- new signals submitted
- active clusters
- locality coverage
- participations (affected + observing)
- create-vs-join ratio
- NLP match rate
- cluster graduation rate
- orphaned cluster rate
- operator review queue size

### 14.3 Mission KPIs (Civic Outcomes)

Track ecosystem outcomes:
- **Loop Closure Rate (LCR)**
- average time to possible restoration
- average time to confirmed resolution
- recurring issue reduction over time
- locality stability improvement
- institution response participation rate
- unresolved cluster age distribution

### 14.4 LCR as Primary Mission Metric

**Loop Closure Rate (LCR)** is the primary internal mission metric.

**Definition:** The percentage of **activated clusters** (clusters that reached `ACTIVE` state)
that subsequently reach **confirmed resolution** (state `RESOLVED` via restoration confirmation
threshold, not decay-only resolution).

> **Correction from v1.0:** LCR was previously defined as "percentage of signals that reach
> confirmed resolution." This was incorrect. Raw signal events are the input to clustering;
> the civic feedback loop is measured at the **cluster level**, not the raw event level. A
> cluster may aggregate dozens of raw signal events; LCR must count clusters, not events.

LCR should be computable at:
- Platform level (all localities)
- Locality level (per ward)
- Category level
- Institution level (for clusters that received an official response)

### 14.5 Future Metrics

The Metrics page should remain extensible for:
- Civic Stability Index (CSI)
- Civic Weather Index (future)
- institutional adoption trend
- locality livability intelligence

These do not all need to be fully built in Phase 3 MVP, but the structure must not block
future addition.

---

## 15. Team & Roles Page

### 15.1 Purpose

Manages internal Hali access and operational permissions.

### 15.2 Canonical Internal Roles

| Role | Summary |
|---|---|
| Super Admin | Full system access, all settings, all audit |
| Operations Admin | Platform health, clusters, routing, localities, daily ops |
| Trust & Integrity Analyst | Integrity queue, anomalies, suspicious bursts, duplicate clusters |
| Institution Manager | Institution onboarding, ownership mapping, coverage gaps, partner coordination |
| Data Analyst | Read-oriented: dashboards and analytics, no destructive controls |

### 15.3 Role Capabilities Matrix

| Capability | Super Admin | Ops Admin | T&I Analyst | Inst. Manager | Data Analyst |
|---|:---:|:---:|:---:|:---:|:---:|
| View all pages | ✓ | ✓ | ✓ | ✓ | ✓ |
| Merge clusters | ✓ | ✓ | ✓ | — | — |
| Suppress clusters | ✓ | ✓ | ✓ | — | — |
| Remap institution ownership | ✓ | ✓ | — | ✓ | — |
| Re-run NLP suggestion | ✓ | ✓ | — | — | — |
| Edit taxonomy mappings | ✓ | — | — | — | — |
| Edit routing rules | ✓ | ✓ | — | — | — |
| Change system settings | ✓ | — | — | — | — |
| Toggle demo mode | ✓ | — | — | — | — |
| Manage roles | ✓ | — | — | — | — |
| View audit logs | ✓ | ✓ | ✓ | ✓ | — |

### 15.4 Schema Note — Admin Role Granularity Gap

The current `account_type` enum in `01_postgres_schema.sql` only contains `admin` as a single
value — it does not represent the five distinct internal roles above.

**Required schema addition** before Phase 3 implementation:

```sql
create table if not exists admin_role_assignments (
    id uuid primary key default gen_random_uuid(),
    account_id uuid not null references accounts(id) on delete cascade,
    admin_role varchar(60) not null,
    -- Values: 'super_admin', 'operations_admin', 'trust_analyst',
    --         'institution_manager', 'data_analyst'
    assigned_by uuid references accounts(id),
    assigned_at timestamptz not null default now(),
    revoked_at timestamptz,
    unique(account_id, admin_role)
);
create index if not exists ix_admin_role_assignments_account on admin_role_assignments(account_id);
```

Authorization must check `admin_role_assignments`, not `account_type` alone. `account_type =
'admin'` is the gate for entering the ops dashboard; the specific role assignment determines
what the operator can do inside it.

---

## 16. Audit Logs Page

### 16.1 Purpose

Audit Logs are essential for governance, traceability, and internal accountability.

### 16.2 Logged Actions

The system must log:
- internal user actions (cluster merge, suppress, remap)
- institution onboarding edits
- routing rule changes
- taxonomy edits
- integrity review actions
- role changes
- settings changes
- demo simulation mode toggles
- simulation toggles in internal environments
- NLP re-run outcomes (accepted or rejected)

### 16.3 Required Log Entry Fields

The canonical audit log schema aligns with `Hali_Admin_Portal_Governance_Integrity_Hardening_Spec.docx` §8:

| Field | Type | Notes |
|---|---|---|
| `action_id` | uuid | Stable log entry ID |
| `actor_id` | uuid | Account ID of operator |
| `actor_role` | varchar(60) | Role at time of action |
| `action_type` | varchar(100) | e.g. `CLUSTER_MERGE`, `CLUSTER_SUPPRESS`, `ROUTING_REMAP` |
| `target_type` | varchar(60) | e.g. `signal_cluster`, `institution` |
| `target_id` | uuid | Target record ID |
| `before_snapshot_hash` | varchar(255) | Hash of before-state |
| `after_snapshot_hash` | varchar(255) | Hash of after-state |
| `reason_code` | varchar(80) | Policy-coded reason (mandatory for suppression) |
| `created_at_utc` | timestamptz | When action occurred |
| `rule_version` | varchar(30) | CIVIS rule family version active at time |

> **Schema patch required:** The current `admin_audit_logs` table in `01_postgres_schema.sql`
> is missing `actor_role`, `before_snapshot_hash`, `after_snapshot_hash`, `reason_code`, and
> `rule_version`. These must be added in a Phase 3 EF Core migration. `metadata jsonb` can
> carry the before/after snapshots as structured content; alternatively, add typed columns.
> The `rule_version` field is critical because historic decisions must be explainable under
> the rule family that was active at the time.

### 16.4 Why It Matters

Essential when: governance questions arise, an institution disputes routing ownership, an
anomaly review needs traceability, or internal misuse needs auditing. Audit logs must be
immutable and exportable.

---

## 17. Settings Page

### 17.1 Purpose

Internal system configuration. Not for casual daily usage. Access restricted to highly
privileged internal roles (Super Admin for sensitive groups; Operations Admin for routing
and locality config).

### 17.2 Settings Groups

- **Taxonomy mappings** — category / subcategory edits (Super Admin only)
- **Routing rules** — institution ownership rule configuration (Super Admin, Ops Admin)
- **Institution ownership rules** — default routing heuristics (Super Admin, Ops Admin)
- **Locality configuration** — ward / corridor / geo-zone management (Ops Admin)
- **Notification routing preferences** — internal alert routing (Ops Admin)
- **CIVIS rule family activation** — toggle which versioned CIVIS rule family is active
  (Super Admin only)

  > **Important constraint:** Settings must never expose raw CIVIS numeric threshold values
  > (base_floor, MACF, WRAB parameters, etc.). The CIVIS Hardening Spec §2 mandates that
  > "exploit-sensitive numeric parameters remain private." Settings may show the name and
  > version of the active rule family and allow toggling between versioned families, but must
  > not render or allow editing of the underlying numeric constants. Those live in environment
  > configuration and code, not in an ops UI.

- **Environment configuration** — read-only display of active environment (MVP / staging /
  production). Not editable via UI.
- **Demo simulation mode** — toggle for non-production environments only (Super Admin only;
  must be hardware-gated so this setting cannot appear in a production environment; see §20)

---

## 18. Internal Operator Actions

Allowed actions in the Hali Ops dashboard:

- Merge duplicate clusters (with audit reason input, outbox event emitted)
- Review integrity alerts (mark reviewed or escalate)
- Remap institution ownership (with audit entry)
- Mark orphaned cluster as onboarding lead
- Inspect routing failures
- Re-run NLP / location suggestion (see §10.5 for full spec)
- Edit taxonomy mappings (Super Admin only)
- Inspect institution response history
- Toggle demo simulation mode in non-production contexts (Super Admin only; see §20)
- Escalate edge cases internally
- Suppress clusters under policy (policy-coded reason mandatory, two-step confirmation,
  outbox event emitted)

Avoid broad manual-public-edit powers. Every state-changing action must emit an audit log
entry. Every action that changes cluster state or suppression status must also emit an outbox
event.

---

## 19. Cluster Detail — Canonical Definition

> **v1.0 duplication resolved:** v1.0 contained two separate cluster detail definitions — §10.4
> and §19 — with different field sets and inconsistent naming. The canonical definition is now
> **§10.4** of this document. The former §19 is removed. All references to cluster detail in
> Hali Ops point to §10.4.

See **§10.4 Cluster Detail Panel** for the full specification, including:
- §10.4A Header
- §10.4B Citizen Signal Summary
- §10.4C Trust / CIVIS Summary
- §10.4D Institution Routing
- §10.4E Official Updates Timeline
- §10.4F Operator Tools (canonical list)
- §10.5 Re-run NLP Suggestion (full spec)
- §10.6 Confidence Band Definition

---

## 20. Real-Time Simulation Behavior (Demo Mode)

### 20.1 Purpose and Scope

Demo mode allows the Hali Ops dashboard to appear live and operationally meaningful during
demonstrations and internal reviews in **non-production environments only**.

Demo mode must be:
- only activatable in environments where `ASPNETCORE_ENVIRONMENT != Production`
- toggled via the Settings page (§17.2) by Super Admin only
- visibly indicated by a persistent banner or badge in the top toolbar when active (see §7.3)
- impossible to activate silently — the UI must never simulate real data without the operator
  knowing demo mode is on

### 20.2 Simulated Event Types

When active, demo mode should simulate:
- new signals emerging in plausible localities and categories
- clusters graduating from unconfirmed to active
- orphaned clusters appearing with no mapped institution
- institution responses being posted
- integrity review items entering the queue
- localities changing status (calm → elevated → active)
- LCR and health metrics updating over time
- some clusters resolving while others age without resolution

### 20.3 Dynamic But Believable

Avoid: constant meaningless motion, random fake chaos, over-noisy event spam.

Use believable cadence:
- active localities shift gradually
- orphaned issues occasionally appear
- institutions engage selectively
- some clusters resolve while others age

### 20.4 Not Everything Must Be Healthy

The dashboard should show both useful system progress and meaningful operational problems.
If everything is perfect, the ops dashboard looks fake and unconvincing.

### 20.5 Data Isolation

Demo mode simulation data must never be written to the production database. Use either:
- an entirely separate simulation data store, or
- in-memory simulation state that is not persisted

---

## 21. Required Admin API Surface

The Hali Ops dashboard requires a set of backend API routes beyond what the existing
`/v1/admin/*` surface covers. The following are the Hali Ops-specific additions required
for Phase 3.

All routes require `admin` account type and role-appropriate authorization.

### 21.1 Overview & Platform Health

| Method | Path | Purpose |
|---|---|---|
| GET | `/v1/admin/overview` | KPI row: active clusters, new signals, orphaned clusters, integrity queue size, LCR |
| GET | `/v1/admin/health` | System health: worker lag per queue, error rates, queue depths |
| GET | `/v1/admin/pulse` | Platform pulse feed — paginated recent system-wide events |

### 21.2 Signal Operations

| Method | Path | Purpose |
|---|---|---|
| GET | `/v1/admin/clusters` | Filterable cluster list (category, state, locality, confidence band, orphaned, flagged) |
| GET | `/v1/admin/clusters/{clusterId}` | Full cluster detail including CIVIS summary, routing, official updates |
| GET | `/v1/admin/clusters/{clusterId}/assessments` | CIVIS assessment history for a cluster |
| GET | `/v1/admin/clusters/{clusterId}/timeline` | Raw event timeline for a cluster |
| POST | `/v1/admin/clusters/{clusterId}/merge` | Merge into another cluster; writes audit + outbox |
| POST | `/v1/admin/clusters/{clusterId}/suppress` | Policy-coded suppression; writes audit + outbox |
| POST | `/v1/admin/clusters/{clusterId}/remap-institution` | Reassign institution owner; writes audit |
| POST | `/v1/admin/clusters/{clusterId}/nlp-rerun` | Trigger NLP re-run; returns diff for operator review |
| POST | `/v1/admin/clusters/{clusterId}/orphan-lead` | Tag as institutional outreach lead |
| POST | `/v1/admin/clusters/{clusterId}/mark-reviewed` | Clear from queue; writes audit |

### 21.3 Localities

| Method | Path | Purpose |
|---|---|---|
| GET | `/v1/admin/localities` | Locality health list with state, active cluster count, orphaned count, LCR |
| GET | `/v1/admin/localities/{localityId}` | Locality detail: clusters, recurring categories, institution coverage, LCR trend |

### 21.4 Institutions

| Method | Path | Purpose |
|---|---|---|
| GET | `/v1/admin/institutions` | Institution list with status, coverage, response stats |
| GET | `/v1/admin/institutions/{institutionId}` | Institution detail |
| GET | `/v1/admin/institutions/coverage-matrix` | Category × locality ownership grid |
| GET | `/v1/admin/orphaned-clusters` | Clusters with no institution mapped, filterable by locality and category |

### 21.5 Integrity

| Method | Path | Purpose |
|---|---|---|
| GET | `/v1/admin/integrity/queue` | Open integrity review items with reason codes and severity |
| POST | `/v1/admin/integrity/{itemId}/resolve` | Mark reviewed or escalate |

### 21.6 Metrics

| Method | Path | Purpose |
|---|---|---|
| GET | `/v1/admin/metrics/product` | Product KPIs: graduation rate, create-vs-join, NLP match rate, etc. |
| GET | `/v1/admin/metrics/mission` | Mission KPIs: LCR, time-to-resolution, locality stability |
| GET | `/v1/admin/metrics/lcr` | LCR breakdown by locality, category, institution, time window |

### 21.7 Audit

| Method | Path | Purpose |
|---|---|---|
| GET | `/v1/admin/audit-logs` | Paginated audit log with filters |

### 21.8 Settings

| Method | Path | Purpose |
|---|---|---|
| GET | `/v1/admin/settings` | Read current settings (Super Admin) |
| PATCH | `/v1/admin/settings/taxonomy` | Edit taxonomy mappings |
| PATCH | `/v1/admin/settings/routing-rules` | Edit routing rules |
| PATCH | `/v1/admin/settings/civis-rule-family` | Switch active CIVIS rule family version (no raw params) |
| POST | `/v1/admin/settings/demo-mode` | Toggle demo mode (non-production only) |

### 21.9 Team & Roles

| Method | Path | Purpose |
|---|---|---|
| GET | `/v1/admin/team` | List internal users and their role assignments |
| POST | `/v1/admin/team/{accountId}/roles` | Assign a role |
| DELETE | `/v1/admin/team/{accountId}/roles/{role}` | Revoke a role |

---

## 22. Responsive Behavior

### 22.1 MVP Priority

Primary experience is desktop.

### 22.2 Desktop Requirements

Desktop should feel executive-ready, scan-friendly, information-rich, not visually oppressive.

### 22.3 Mobile

Mobile fallback may exist, but no deep mobile optimization is required for Phase 3 MVP.
Hali Ops is an internal workstation surface first.

---

## 23. What Must Never Happen

- Internal operators directly overriding public civic reality without audit trails and
  policy-coded reasons
- Orphaned clusters disappearing from internal visibility
- Integrity review reduced to aesthetic flag lists with no actionable path
- Institution coverage gaps hidden or de-prioritized
- Civic data treated like support tickets
- Product health metrics and mission outcome metrics mixed without visual distinction
- Any admin action with no auditability
- Raw CIVIS numeric scores or threshold parameters exposed in any UI surface
- A cluttered enterprise UI that destroys signal legibility
- False perfection in demo mode where no risk or anomaly ever appears
- Demo mode active in a production environment

---

## 24. MVP Success Criteria

The Hali Ops Phase 3 MVP is successful if:

1. An internal operator instantly understands system health on landing at the Overview
2. Platform integrity issues are visible and actionable without requiring deep navigation
3. Orphaned clusters and coverage gaps are immediately obvious
4. Institution ownership and routing are legible at a glance
5. Localities can be understood as living civic systems with trend data
6. Product KPIs and mission KPIs are clearly distinct and both present
7. Every operator action that changes system state is auditable and traceable
8. The dashboard makes Hali feel like real civic infrastructure, not just an app

---

## 25. Final Instruction for Claude / Builders

When generating or modifying Hali Ops features, always ask:

**Does this improve system stewardship, trust, routing clarity, and the civic feedback loop?**

If not, reject it.

When forced to choose between more controls, more cleverness, more complexity, or more system
legibility — choose:

- legibility
- trust
- auditability
- operational clarity
- principled restraint

---

## 26. Implementation Checklist (Phase 3)

Before beginning Phase 3 implementation, confirm the following are in place:

- [ ] `admin_role_assignments` table added via EF Core migration (§15.4)
- [ ] `admin_audit_logs` schema patched to add `actor_role`, `before_snapshot_hash`,
      `after_snapshot_hash`, `reason_code`, `rule_version` (§16.3)
- [ ] Admin API surface from §21 added to OpenAPI spec before frontend work begins
- [ ] Confidence band logic implemented server-side; no raw CIVIS scores in any API response
- [ ] Demo mode gated by environment check in API and settings handler
- [ ] Outbox events confirmed for: cluster suppress, cluster merge, routing remap
- [ ] CIVIS rule family versioning confirmed in `civis_decisions` table before Settings
      rule-family toggle is wired

---

## 27. Status

This document is the **canonical Hali Ops internal dashboard specification v2.0**, covering:

- internal control center purpose and Phase 3 placement
- platform oversight structure
- trust and integrity operations
- institution coverage logic
- locality intelligence (TDA-connected)
- metrics and governance surfaces
- roles, permissions, and required schema additions
- audit log canonical schema
- demo mode specification
- confidence band definition
- NLP re-run operator action specification
- complete Admin API surface for Phase 3

Citizen app and institution dashboard are maintained as separate canonical specs.

---

*END OF FILE*
