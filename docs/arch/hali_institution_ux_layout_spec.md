# Hali Institution Dashboard — Canonical UX + Layout Spec

**Phase:** 1.5. **Issue:** #233. **Scope:** institution operational
dashboard (Phase 2). Institution-admin and Hali-ops shells inherit
from this spec with the explicit extensions noted in §8.

This spec is enforceable. Phase 3 implementation must match every
section below; deviations require a PR-level decision with an updated
entry in this document.

**Tokens** are referenced by name (e.g. `Spacing.lg`, `Radius.default`,
`InstitutionColors.primary`) from `@hali/design-system`. Raw values
are not duplicated here — the design system is the source of truth.

**Cross-references:**
- `docs/reference-ui/v0/phase-1.5-visual-audit.md` — screen-level
  citations behind each rule.
- `docs/arch/SECURITY_POSTURE.md` §2 — scope enforcement.
- `docs/arch/OBSERVABILITY_MODEL.md` §5 — required telemetry per page.
- `docs/arch/FEATURE_FLIGHTING_MODEL.md` — how dark-launch and
  pilot flags interact with the UX.

---

## 1. Shell structure

The institution shell is a fixed-width left sidebar + a content
region with a sticky topbar. Dimensions:

| Region | Value | Token |
|---|---|---|
| Sidebar width | 224px (`w-56`) | inline value — no design-system token yet |
| Sidebar brand header height | 64px | inline value (aligned with topbar height) |
| Topbar height | 64px | inline value — no design-system token yet |
| Content region horizontal padding | `Spacing.2xl` (24px) | shared |
| Content region vertical padding | `Spacing.xl` (20px) top, `Spacing.4xl` (48px) bottom | shared |
| Max content width | unbounded; grid columns cap at 4 | — |

Layout dimensions that are currently inline (sidebar/topbar sizes)
may graduate to design-system tokens in a future revision; the
synthesis keeps them inline until there is a second consumer.

The sidebar is `position: fixed`, full viewport height, `z-index: 40`,
`border-right: 1px solid InstitutionColors.sidebarBorder`, background
`InstitutionColors.sidebar`.

The topbar is `position: sticky` inside the content region (not
above the sidebar), `z-index: 30`, background
`InstitutionColors.background/95` with `backdrop-filter: blur(6px)`.

---

## 2. Sidebar

### Brand header (top of sidebar)

- Height 64px, `border-bottom`, horizontal padding 20px.
- Primary-filled 8×8 (32px) tile with radius `Radius.md`, centred
  letter "H" in `FontFamily.sans` `FontWeight.bold`
  `InstitutionColors.primaryForeground`.
- Wordmark "Hali" in `FontSize.cardTitle` (15px)
  `FontWeight.semiBold` `InstitutionColors.sidebarForeground`.

### Nav area

- `flex-1`, padding `Spacing.md` (12px).
- Items rendered as a vertical `<ul>` with 4px gap between items.
- Each item: full width, `Radius.md` corners, `Spacing.md` horizontal
  padding, 10px vertical padding, icon (16×16) + label, `FontSize.body`
  (14px) `FontWeight.medium`.
- **Active state:** background `InstitutionColors.sidebarAccent`,
  foreground `InstitutionColors.sidebarAccentForeground`.
- **Inactive state:** foreground
  `InstitutionColors.sidebarForeground` at 70% opacity; hover
  restores to 100% with a subtle `sidebarAccent/50` background.

### Footer

- Border-top, padding `Spacing.lg` (16px).
- Institution name (`FontSize.badge` / 12px, muted) + app version
  (`FontSize.micro` / 10px, more muted).

### Canonical nav set

```
[icon LayoutDashboard] Overview      → /overview
[icon Radio]           Live Signals  → /signals
[icon MapPin]          Areas         → /areas
[icon BarChart3]       Metrics       → /metrics
```

Routes are relative to the authenticated institution scope; the
institution id is not in the URL — it is resolved server-side from
the JWT claim (see `docs/arch/SECURITY_POSTURE.md` §2).

---

## 3. Topbar

From left to right:

1. **Institution picker** — dropdown button.
   - **Visible** only when the authenticated user has membership in
     more than one institution. Otherwise render as a static label:
     institution name + category description.
   - **Chevron affordance** only when picker is visible.
   - Changing selection navigates the content region without full
     page reload (client-side route change).
2. **Area picker** — dropdown button.
   - Options derived from the authenticated user's
     `institution_user_scopes` rows server-side; never a hardcoded
     list.
   - Default selection: "All Areas".
   - Chevron affordance always visible.
3. **Category description label** (read-only) — shows the category
   or categories this institution owns (e.g. "Power" for Kenya Power,
   "Drainage & Waste" for Nairobi County). Derived server-side.
4. **Spacer** — `flex-1`.
5. **Search input** — global search across signals in scope.
6. **Notifications bell** with unread-count badge.
7. **Refresh activity** button with a 2s "Updated" confirmation
   toast.
8. **Account menu** — avatar / initials with dropdown.

Every action above emits a `institution_web.*` event per
`docs/arch/OBSERVABILITY_MODEL.md` §5.

---

## 4. Content region rules

Every page follows this vertical rhythm:

1. **Page header** — `<h1>` `FontSize.title` (20px)
   `FontWeight.semiBold` + subheader `FontSize.body` (14px)
   `SharedSemanticColors.mutedForeground`.
   - Subheader always reflects the current scope filters (institution
     + area).
2. **Primary content** — page-specific, scrolls vertically.
3. **Activity feed** — either inline as a right rail (Overview) or
   surfaced via a collapsible side panel (Live Signals, Signal
   Detail).

Vertical gap between blocks: `Spacing.2xl` (24px).

---

## 5. Page inventory (Phase 2 scope)

### 5.1 Overview

- Page header: "Overview" + subheader.
- 4-up summary card grid (see §6.1). Cards are drill-down affordances
  scoped to the current institution + area.
- Area status grid: 6 area tiles max, ranked by activity; see §6.2.
- Activity feed inline below the area grid on narrower viewports,
  or as a right rail on viewports ≥1280px (`xl:`).

### 5.2 Live Signals

- Page header: "Live Signals" + subheader including total count in
  scope.
- Filter chip row: `All`, `Active`, `Growing`, `Needs attention`,
  `Restoration`. Single-select.
- Signal list: vertically stacked signal cards (see §6.3). Pagination
  cursor-based (see §7 of `hali_institution_backend_contract_implications.md`).
- Empty state: calm-message ("No signals in scope" + timestamp).

### 5.3 Signal Detail

- Page header: signal title + area label + category badge.
- **Right column — updates timeline** showing every posted official
  update in chronological order. "Post update" CTA at the top of
  the timeline.
- **Left column — signal metadata:** description, citizen summary,
  affected count, recent reports (last 24h), location string, trend
  arrow, response status, restoration status.
- Restoration-waiting-confirmation banner (see §6.7) when applicable.

### 5.4 Areas

- Page header: "Areas" + subheader.
- Table or card grid of every area in scope with condition, active
  signals count, top category.
- Clicking an area navigates to Live Signals with the area filter
  applied.

### 5.5 Metrics

- Page header: "Metrics" + subheader.
- Dashboard tiles sourced from the observability pipeline (see
  `docs/arch/OBSERVABILITY_MODEL.md` §8). Exact tile set is **requires-decision**
  — the Phase 2 implementation PR adds tiles incrementally.

---

## 6. Canonical layout patterns

### 6.1 Summary card (4-up grid)

- `grid-template-columns: repeat(4, minmax(0, 1fr))`, gap `Spacing.lg`
  (16px).
- Each card: padding `Spacing.lg`, `Radius.default` (12px),
  border `SharedSemanticColors.border`, background
  `SharedSemanticColors.card`.
- Card contents: icon (20px) + title (`FontSize.badge` / 12px,
  `FontWeight.medium`, uppercase, tracking-wide,
  `SharedSemanticColors.mutedForeground`) + value
  (`FontSize.title` / 20px, `FontWeight.semiBold`,
  `InstitutionColors.foreground`).
- Drill-down affordance: entire card is clickable; chevron appears
  on hover.
- Variant accent colour on the icon container:
  - `default` → primary
  - `warning` → amber
  - `info` → sky
  - `success` → emerald
- Empty-state behaviour: render value "0" with muted foreground.

### 6.2 Area status grid

- `grid-template-columns: repeat(3, minmax(0, 1fr))` at viewports
  ≥`md` breakpoint; `repeat(2, …)` at `sm`; `1fr` stacked at `xs`.
- Each tile: padding `Spacing.lg`, `Radius.default`, 1px border,
  background card.
- Tile content: area name (`FontSize.cardTitle` /
  `FontWeight.semiBold`) + condition pill + active-signals count +
  top-category label.
- Clicking navigates to Live Signals filtered to that area.

### 6.3 Signal card (list view)

- Full-width card, padding `Spacing.lg`, `Radius.default`, border.
- Row 1: category badge + area label + condition pill + trend arrow.
- Row 2: title (`FontSize.cardTitle` / `FontWeight.semiBold`).
- Row 3: affected count + recent reports + response-status pill.
- Row 4 (on hover): "View detail" + "Post update" inline actions.
- Click anywhere navigates to Signal Detail; inline action buttons
  stop propagation.

### 6.4 Filter chip row

- Horizontal flex row above a list.
- Each chip: pill-shape (`Radius.full`), `Spacing.md` horizontal
  padding, 8px vertical, `FontSize.badge`, `FontWeight.medium`.
- Single-select. Active chip uses primary accent; inactive uses
  muted-foreground.

### 6.5 Search input

- Standard form input pattern; placeholder "Search signals…".
- Debounce 250ms before dispatching a query.
- ESC clears the input and restores the unfiltered list.

### 6.6 Activity feed item

- Horizontal layout: type-icon (16×16) + message + timestamp.
- Message: `FontSize.body` (14px). Timestamp: `FontSize.micro` (10px),
  muted.
- Types: `new_signal`, `growing`, `stabilising`, `update_posted`,
  `restoration`, `restored` — each with a distinct icon.
- Clicking an item navigates to the originating Signal Detail.

### 6.7 Restoration-waiting-confirmation banner

- Rendered at the top of Signal Detail when
  `responseStatus = "Restoration in progress"` and citizen
  confirmation has not yet reached the threshold defined in
  `CLAUDE.md` product doctrine (≥60% + ≥2 affected votes).
- **Visual:** neutral info treatment (not success, not warning) —
  the institution has acted but the citizen gate has not resolved.
  Confirmed per Phase 1.5 review gate.
- **Copy:** "Awaiting confirmation from affected users" (primary
  line). Secondary line renders the current ratio + affected-vote
  count when available.

### 6.8 Post-update modal

- Entry: "Post update" CTA in Signal Detail + inline in Live Signals
  card.
- Rendered as a centred modal at `Radius.lg` (16px).
- Fields: **update kind** (live_update / scheduled_disruption /
  advisory_public_notice — see Phase 2 backend §2), **message**,
  **affected areas**, **expected resolution** (optional, stage-
  dependent).
- Submit triggers `POST /v1/official-posts`; dismiss closes without
  persistence.
- After success, timeline refreshes optimistically.

### 6.9 Notification centre

- Popover anchored to the notifications bell.
- Each item: type-icon + message + timestamp + mark-read affordance.
- Clicking navigates to the originating signal if `signalId` is
  present; otherwise no-op.
- "Mark all as read" action at the top.

---

## 7. State and empty states

- **Loading** — skeleton blocks sized to match the final content
  layout (no spinners for above-the-fold content).
- **Empty** — calm neutral copy; never an illustration. Tone: "No
  signals in {scope} in the last {window}. Last refresh {timestamp}."
- **Error** — canonical typed error envelope surfaced as a full-page
  alert with retry button. Error code rendered at the bottom for
  support.

---

## 8. Institution-admin extension (Phase 2 scope-adjacent)

The institution-admin surface **reuses this shell with subtle visual
separation** — confirmed per Phase 1.5 review gate. The separation
must be enough to make "I am in admin context" unambiguous without
re-creating a distinct shell.

Chrome differences:

- An additional sidebar group **"Administration"** below the main
  operational nav, visually separated by a divider + a muted-
  foreground group label ("Administration"), the same treatment
  shadcn sidebar groups use.
- Admin nav items: **Users**, **Roles & scopes**, **Audit log**.
- When the active route is inside an admin group, the sidebar
  brand tile switches to a subtle variant — same primary colour but
  with a small "Admin" pill rendered adjacent to the "Hali"
  wordmark (`FontSize.micro`, muted-foreground, uppercase). The
  brand tile colour is not changed — only the label annotation.
- The institution picker in the topbar is hidden for admin views
  (admin work is always scoped to a single institution).
- Topbar otherwise unchanged — area picker remains available so
  admin actions can be scoped.
- Step-up auth requirement for privileged admin actions is enforced
  server-side per `docs/arch/SECURITY_POSTURE.md` §1; the client
  surfaces the step-up challenge when a privileged action is
  attempted without a fresh second-factor timestamp.

Hali-ops surface (Phase 3) extends further; out of Phase 1.5 scope.

---

## 8.5 Session timeout UX (soft warning + hard timeout)

Confirmed per Phase 1.5 review gate. Two-tier session enforcement
aligned with `docs/arch/SECURITY_POSTURE.md` §3:

### Soft warning

- Triggered when the idle session has **3 minutes remaining before
  the 30-minute idle timeout** (i.e. 27 minutes of inactivity).
- Non-blocking banner rendered at the top of the content region
  (below the topbar). Neutral info treatment, not a modal.
- **Copy (primary):** "Your session will end in 3 minutes due to
  inactivity."
- **Action:** a "Stay signed in" button that issues a keep-alive
  request; successful response dismisses the banner and resets
  the idle timer.
- Banner persists until dismissed or the idle timer expires.

### Hard timeout

- Triggered at the 30-minute idle timeout **or** at the 12-hour
  hard session expiry (whichever comes first).
- Client clears the session state and redirects to the login
  screen with an unobtrusive notice: "Your session ended. Please
  sign in to continue."
- Any in-flight form state is preserved in session storage (not
  localStorage) keyed by route and replayed on re-authentication
  so the user does not lose unsubmitted work on a dropped session.
- Write endpoints hit after expiry return `401` with the canonical
  error envelope; the client always honours the server's 401 even
  if the client clock says the session is still alive.

### Step-up auth for privileged actions

- Independent of idle/hard timeout; triggered when the user attempts
  a privileged admin action whose step-up timestamp is older than
  the server's policy window.
- Rendered as a modal prompt requesting the second factor (TOTP).
- On success, the step-up timestamp is refreshed and the originally
  attempted action replays automatically.

---

## 9. Accessibility floor

- Keyboard: sidebar and topbar are fully tab-navigable; active
  nav item receives focus on page load.
- Every icon-only button has an `aria-label`.
- Focus ring uses `InstitutionColors.ring` with 2px outline.
- Colour contrast: text on background must meet WCAG AA at minimum.
  The condition pills are accompanied by text (never colour alone).

---

## 10. Review-gate decisions (now binding)

Every requires-decision item raised during synthesis was resolved
at the Phase 1.5 review gate. Each decision below is now binding
on Phase 3 implementation:

| Decision | Resolution |
|---|---|
| Institution picker visibility | Visible only for multi-institution users; otherwise a static label (see §3). |
| Area picker source | Server-driven from the authenticated user's institution scope — never hardcoded (see §3). |
| Institution-admin shell | Reuses this shell with **subtle visual separation** — divider + "Administration" group label + a muted "Admin" pill next to the wordmark (see §8). |
| Category taxonomy | Use the backend's 8 canonical `CivicCategory` values (`roads`, `transport`, `electricity`, `water`, `environment`, `safety`, `governance`, `infrastructure`). Any v0 → canonical mapping is documented in `docs/arch/hali_institution_backend_contract_implications.md` §4. |
| Update kinds | All three canonical kinds supported in the post-update modal: `live_update`, `scheduled_disruption`, `advisory_public_notice` (see §6.8). |
| Restoration banner | Neutral tone, copy "Awaiting confirmation from affected users" (see §6.7). |
| Destructive hue | Shared semantic base with surface-level rendering differences (citizen keeps its warmer hue; institution keeps its sharper hue). Not fully unified. See `@hali/design-system/tokens/colors.ts`. |
| Session UX | Soft warning at 27 minutes idle + hard timeout at 30 min idle or 12 h hard expiry (see §8.5). |

These decisions must not be relitigated without an explicit PR-level
revisit to this document.
