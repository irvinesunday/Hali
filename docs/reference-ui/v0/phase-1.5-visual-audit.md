# Phase 1.5 Visual Audit

**Issues:** #232. **Purpose:** make the Phase 1.5 synthesis review
tractable. This document cites every pattern against a specific v0
screen so the reader is not reading abstract descriptions — they are
checking specific claims against specific source files.

**Classification rule** (per design intent clarification):

- Colour / tone / visual-weight differences → intentional surface
  theming. Exception: structural chrome tokens (`border`, `muted`,
  `input`, `background`) are unified unless there is a reason they
  differ.
- Spacing / radius / typography scale differences → erroneous by
  default. Exception: density-justified differences (institution
  surfaces are inherently denser than mobile surfaces) are
  acceptable if consistent with that pattern.
- Anything else → **requires-decision**. Surfaced at the human gate.

**Artifact paths:** all citations below refer to files inside the
tracked zip artifacts under `docs/reference-ui/v0/`. For brevity
the form `institution:components/dashboard/sidebar.tsx` is shorthand
for `docs/reference-ui/v0/v0-hali-institution-ui.zip:components/dashboard/sidebar.tsx`; citizen citations are shortened the same way.

---

## 1. Shell structure

### Dashboard shell (institution)

- **Fixed left sidebar, width `w-56` (14rem / 224px), full viewport
  height, z-40, border-right** — `institution:components/dashboard/sidebar.tsx:20`.
- **Brand header (64px high, border-bottom, 20px horizontal
  padding)** with an 8×8 primary-filled tile containing the letter
  "H" and the wordmark "Hali" in `text-lg font-semibold` —
  `institution:components/dashboard/sidebar.tsx:21–26`.
- **Nav area** occupies `flex-1` with 12px padding; items are
  `rounded-lg px-3 py-2.5 text-sm font-medium` buttons —
  `institution:components/dashboard/sidebar.tsx:27–50`.
- **Sidebar footer** with institution name + version placeholder —
  `institution:components/dashboard/sidebar.tsx:52–55`.
- **Content region** is everything to the right of the sidebar with
  the topbar pinned at the top — composed in
  `institution:app/page.tsx`.

Evidence strength: **strong** — single canonical shell file.

### Mobile shell (citizen)

- **No sidebar.** Header is inline at the top of each screen with
  the app name, locality selector, and section scroll container —
  `citizen:components/home-screen.tsx:~35`.
- **Floating Action Button (FAB)** for "Report a Signal" — persistent
  across screens — `citizen:components/floating-action-button.tsx`.
- **Bottom-anchored navigation** originally existed in v0
  (`citizen:components/bottom-nav.tsx`) but the citizen mobile app
  now uses Expo Router file-based tabs; the shell shape carries
  forward even though the v0 component doesn't.

**Mobile → institution carryover:** the shell concepts don't carry
forward at all — citizen is a scrollable vertical stack with no
fixed navigation chrome (beyond the FAB), while institution has a
persistent sidebar + topbar. **Justified** — surfaces serve different
use cases and device form factors.

---

## 2. Navigation model

### Institution nav set

Four top-level routes, in order:

1. **Overview** — `LayoutDashboard` icon — `institution:components/dashboard/sidebar.tsx:11`
2. **Live Signals** — `Radio` icon — same file:12
3. **Areas** — `MapPin` icon — same file:13
4. **Metrics** — `BarChart3` icon — same file:14

Each nav item renders a full-width button with icon + label. Active
state uses `bg-sidebar-accent text-sidebar-accent-foreground`;
inactive uses `text-sidebar-foreground/70` with a hover affordance.

Evidence: **strong** — single source of truth in
`institution:components/dashboard/sidebar.tsx`.

### Institution admin nav

The v0 institution artifact does **not** include an institution-admin
surface. Admin-level screens (user/role/scope management) are
**requires-decision** for Phase 1.5 — the synthesis assumes they
reuse the same shell with an additional nav item, but this needs
confirmation.

### Mobile nav

Citizen mobile uses Expo Router file-based tabs
(`apps/citizen-mobile/app/(app)/`). Not comparable to the institution
sidebar.

---

## 3. Topbar (institution)

- **Sticky, full-width, inside the content region (not above the
  sidebar)**, fixed height — `institution:components/dashboard/topbar.tsx`.
- **Institution picker** — DropdownMenu with 4 institutions:
  "Kenya Power", "Nairobi Water", "KenHA / KURA", "Nairobi County" —
  `topbar.tsx:23–28`.
- **Area picker** — DropdownMenu with 7 areas — `topbar.tsx:30–38`.
- **Notifications bell** with popover showing recent notifications —
  `topbar.tsx` uses `Popover` + `ScrollArea` components.
- **Refresh activity** button with 2s visual feedback — same file.
- **Category description** shown next to the institution (e.g.
  Kenya Power → "Power", Nairobi County → "Drainage & Waste") —
  `topbar.tsx:64–74`.

Evidence: **strong**.

**Requires decision — Phase 1.5:** the v0 topbar hardcodes a demo
set of institutions and areas. In production:

- Institution picker should only appear for users with membership in
  more than one institution. Single-institution users should see the
  institution name as a label, not a picker.
- Area picker scope must be derived from the authenticated user's
  jurisdiction scope (see `docs/arch/SECURITY_POSTURE.md` §2) rather than a
  hardcoded list.

---

## 4. Content region

### Overview page

Five visual blocks stacked with `gap-6` (24px):

1. **Page header** — H1 "Overview" (`text-2xl font-semibold`) + a
   subhead with the institution name and area filter — `institution:components/dashboard/overview.tsx:72–77`.
2. **4-column summary card grid** — Active Signals, Growing Signals,
   Updates Posted Today, Stabilized Today. Each card is clickable and
   drills down to Live Signals filtered — `overview.tsx:80–108`.
3. **Area status grid** — up to 6 area cards showing condition
   (Active / Elevated / Calm) and active-signal count, filtered to
   relevant-to-institution — `overview.tsx:40–67`.
4. **Activity feed** (right-rail or below area grid) — recent
   "X gaining reports", "Y stabilising" items — `overview.tsx` plus
   `activityFeed` prop.

Evidence: **strong**.

### Live Signals page

Full-width list view with filter affordances and drilldown into
Signal Detail — `institution:components/dashboard/live-signals.tsx`
(307 lines). Primary patterns:

- Per-signal card with condition, category, area, affected count,
  trend, and response status.
- Drill-in click opens Signal Detail.
- "Post update" button per signal opens post-update-modal.
- Filter chips at the top of the page (active / growing / needs
  attention).

Evidence: **strong**.

### Signal Detail page

- Full-view signal with title, description, citizen summary,
  affected count, recent reports, location string.
- Update timeline showing every previously-posted official update.
- "Post new update" CTA opens the post-update modal.
- Restoration action when cluster is in a restoration-ready state.

Evidence: **strong** — `institution:components/dashboard/signal-detail.tsx` (352 lines).

### Areas + Metrics

Thinner pages — Areas shows area-level roll-up, Metrics shows simple
analytics. Evidence: **strong** for shape, but specific metrics
tile-set is **requires-decision** against real backend data.

---

## 5. List / card / filter patterns

### Summary card (4-up grid)

- Fixed aspect ratio across the 4-column grid — `overview.tsx` uses
  `grid grid-cols-4 gap-4`.
- Each card exposes a prop `variant` (`default`, `warning`, `info`,
  `success`) that scopes the accent colour — extracted from
  `overview.tsx:80–108` + sub-component.
- Card is clickable and calls a `onDrilldown(filterType)` handler.

### Signal card (list view)

- Title, category badge, condition pill, affected-count metric,
  trend arrow, area label, response status.
- Click opens Signal Detail.
- "Post update" inline action.

Evidence: `institution:components/dashboard/live-signals.tsx`.

### Activity feed item

- Timestamp + message + type-dependent icon (growing / stabilising /
  update / restoration).
- Typography: message in `text-sm`, timestamp in
  `text-xs text-muted-foreground`.

Evidence: `institution:app/page.tsx` + `activity` data shape.

### Filter pattern

The v0 institution uses filter chips (pill-shaped buttons with a
selected state) above the Live Signals list. **Assumption** — no
dedicated filter component file; the shape is inferred from
`live-signals.tsx` layout. Flagged as **assumption** in the synthesis.

---

## 6. Official-update creation workflow

- Entry points: "Post update" CTA inside Signal Detail + inline in
  Live Signals list.
- Modal-based flow (not a separate page) — `institution:components/dashboard/post-update-modal.tsx` (350 lines).
- Fields: update stage (response status), message, affected areas,
  expected resolution time.
- Close on submit or cancel.

Evidence: **strong**.

**Requires decision — Phase 1.5:** the canonical backend supports
three update kinds (Live Update, Scheduled Disruption, Advisory /
Public Notice — see `CLAUDE.md` Phase 1 scope and
`02_openapi.yaml` `/v1/official-posts`). The v0 post-update modal
only models the response-stage progression (a narrower concept).
Scheduled Disruption + Advisory creation flows are **not** present
in v0 and are **requires-decision** for Phase 1.5.

---

## 7. Restoration workflow

- Triggered from the Signal Detail when the cluster is eligible
  (v0 demo marks clusters as "Possible restoration" or
  "Restoration in progress").
- An institution actor "claims" restoration → cluster transitions
  to `possible_restoration` (backend), which then requires citizen
  confirmation per the canonical restoration rule (see
  `CLAUDE.md` product doctrine).

Evidence: **weak** — v0 surfaces restoration as a response status
but does not isolate the citizen-confirmation gate visually. Flagged
as **assumption**.

**Requires decision — Phase 1.5:** the explicit UX pattern for the
"waiting on citizen confirmation" state — a dedicated banner or
badge on the Signal Detail once the institution has claimed
restoration but confirmation has not yet been reached. Phase 1.5
synthesis proposes such a pattern.

---

## 8. Mobile → institution carryover flags

| Carryover | Mobile evidence | Institution evidence | Verdict |
|---|---|---|---|
| Geist font family | `citizen:app/layout.tsx` | `institution:app/layout.tsx` | **Justified** — shared Hali wordmark and brand typography |
| Primary brand tile ("H" mark) | Header strip in `citizen:components/home-screen.tsx` | Sidebar brand tile in `institution:components/dashboard/sidebar.tsx:22–25` | **Justified** — brand anchor across surfaces |
| Condition badge palette | `citizen:components/issue-card.tsx` explicit map | Signal list in `institution:components/dashboard/live-signals.tsx` uses the same Tailwind colour classes | **Justified** — shared semantic vocabulary for citizens and institution-side viewers |
| Signal / cluster concept | "Issue" in citizen v0 | "Signal" in institution v0 | **Requires-decision** — naming is inconsistent in v0. Backend uses "SignalCluster" for public view. Phase 1.5 recommends standardising on "Signal" for institution surface to match backend emerging vocabulary |
| Category taxonomy | 8 canonical `CivicCategory` values in backend (roads, transport, electricity, water, environment, safety, governance, infrastructure) | v0 institution uses 6 values (Power, Water, Roads, Traffic, Drainage, Waste) mapped to 4 institutions | **Speculative** — v0 taxonomy is a demo simplification. Phase 1.5 must reconcile against the 8 canonical backend categories; Phase 2 institution backend (#195) will validate category membership server-side |
| Primary colour hue | `oklch(0.55 0.12 190)` warmer/softer | `oklch(0.65 0.12 180)` cooler/sharper | **Justified** — per design intent clarification, intentional surface theming. Model as shared semantic + surface overrides |
| Destructive hue | `oklch(0.60 0.15 30)` | `oklch(0.577 0.245 27.325)` | **Justified** per review gate — shared semantic base, surface-level rendering differences. Each surface keeps its v0 hue; the destructive foreground is converged to near-white for legibility on both |
| Radius default | `0.75rem` | `0.75rem` | **Justified** — identical |
| Border colour | `oklch(0.90 0.01 200)` | `oklch(0.92 0.01 180)` | **Erroneous** — structural chrome should be unified per the classification rule; synthesis picks the mid-value |
| Muted colour | `oklch(0.95 0.005 200)` | `oklch(0.96 0.008 180)` | **Erroneous** — same rule; synthesis unifies |

---

## 9. Assumption register

Items that the synthesis depends on but where v0 evidence is weak,
ambiguous, or absent:

| Item | Why |
|---|---|
| Filter component pattern | No dedicated v0 component file; shape inferred from `live-signals.tsx` |
| Institution-admin surface | Not present in v0; synthesis assumes same shell + extra nav group |
| Hali-ops surface | Not present in v0; Phase 3 work |
| Restoration "waiting on citizen confirmation" UX | v0 demo doesn't differentiate this state visually |
| Scheduled Disruption + Advisory creation UX | v0 only covers response-stage updates |
| Notification-centre exhaustive taxonomy | v0 ships 6 notification types; backend may emit more |
| Dense-metrics dashboard tile set | v0 Metrics page is a demo; real metric set must map to SLO / alert dashboards per `docs/arch/OBSERVABILITY_MODEL.md` §8 |
| Sidebar collapse / responsive behaviour | Not demonstrated in v0 |
| Keyboard + accessibility conventions | Not a v0 focus; Phase 3 implementation must add |
| Session timeout / lock UX | Not present in v0 |

---

## 10. Review-gate decisions (now binding)

Every requires-decision item below was resolved at the Phase 1.5
review gate. Each resolution is binding on Phase 3 implementation;
the rationale is captured for future auditability.

1. **Institution picker visibility** — picker visible only for
   multi-institution users; otherwise static label.
2. **Area picker source** — server-driven from the authenticated
   user's scope; never hardcoded.
3. **Institution-admin surface shell** — same shell with subtle
   visual separation (sidebar group + "Admin" pill adjacent to
   wordmark + hidden institution picker).
4. **Category taxonomy** — canonical 8 `CivicCategory` values
   (`roads`, `transport`, `electricity`, `water`, `environment`,
   `safety`, `governance`, `infrastructure`). v0 → canonical
   mapping documented in
   `docs/arch/hali_institution_backend_contract_implications.md` §4.
5. **Update kinds** — all three canonical kinds supported:
   `live_update`, `scheduled_disruption`, `advisory_public_notice`.
6. **Restoration banner** — neutral tone; primary copy "Awaiting
   confirmation from affected users" + secondary ratio/vote row.
7. **Destructive colour** — shared semantic base with surface-level
   rendering differences (citizen keeps its warmer hue; institution
   keeps its sharper hue). Not fully unified — each surface retains
   its register.
8. **Session UX** — soft warning at 27 minutes idle + hard timeout
   at 30 min idle or 12 h hard expiry. Detail in
   `docs/arch/hali_institution_ux_layout_spec.md` §8.5.

These decisions must not be relitigated without an explicit
PR-level revisit to the spec documents.
