# Hali Institution — Reusable Component Patterns

**Phase:** 1.5. **Issue:** #234. **Scope:** institution operational
web (Phase 2).

This document defines the concrete, reusable component patterns the
institution dashboard consumes. Each pattern lists its v0 origin, its
API surface (props / events / data shape), and its behaviour rules.
Phase 3 implementation imports `@hali/design-system` tokens and
composes these patterns — it does not re-derive.

**Cross-references:**
- `docs/reference-ui/v0/phase-1.5-visual-audit.md` — pattern citations
- `docs/arch/hali_institution_ux_layout_spec.md` — where each pattern
  appears in the shell

---

## 1. Shell primitives

### `InstitutionShell`

Wraps every institution page with sidebar + topbar + content region.

- **Props:** `currentRoute: NavRoute`, `institution: InstitutionSummary`,
  `areaOptions: AreaOption[]`, `children: ReactNode`.
- **Events:** `onRouteChange(route)`, `onAreaChange(areaId)`,
  `onInstitutionChange(institutionId)` (only emitted when user has
  multi-institution membership), `onRefreshActivity()`.
- **Behaviour:** sidebar is always rendered; topbar is sticky; content
  region is scroll-independent. Route changes are client-side.
- **v0 origin:** `institution:components/dashboard/sidebar.tsx` +
  `topbar.tsx` + `app/page.tsx` shell composition.

### `Sidebar`

- **Props:** `nav: NavItem[]`, `activeId: string`, `brand: BrandConfig`,
  `footer: ReactNode`.
- **Events:** `onSelect(id)`.
- **Behaviour:** fixed width 224px, active-state styling per layout
  §2, all items keyboard-navigable.
- **v0 origin:** `institution:components/dashboard/sidebar.tsx`.

### `Topbar`

- **Props:** `institution`, `areaOptions`, `selectedAreaId`,
  `canSwitchInstitution: boolean`, `unreadNotificationCount: number`,
  `isRefreshing: boolean`.
- **Events:** `onAreaChange`, `onInstitutionChange`,
  `onOpenNotifications`, `onRefresh`.
- **Slots:** `accountMenu: ReactNode`.
- **Behaviour:** sticky top, 64px tall, flex row with spacer. Account
  menu slot is consumer-provided so auth state shape stays out of
  this component.
- **v0 origin:** `institution:components/dashboard/topbar.tsx`.

---

## 2. Data display primitives

### `SummaryCard`

4-up grid tile surfacing a single metric with drill-down.

- **Props:** `title: string`, `value: number | string`,
  `icon: LucideIcon`, `variant: "default" | "warning" | "info" | "success"`,
  `delta?: { value: number; direction: "up" | "down" | "flat" }`.
- **Events:** `onClick()` — drill-down affordance.
- **Behaviour:** full-card click target; chevron appears on hover;
  variant applies accent colour to icon container only (not title/value).
- **v0 origin:** `institution:components/dashboard/overview.tsx:80–108`
  (`SummaryCard` sub-component).

### `AreaStatusTile`

Area-level roll-up tile for the Overview and Areas pages.

- **Props:** `area: { id; name; status: "Active"|"Elevated"|"Calm";
  activeSignals: number; topCategory: string | null; lastUpdatedAt: string }`.
- **Events:** `onClick(areaId)` — navigates to Live Signals filtered
  by area.
- **Behaviour:** condition pill uses `ConditionBadgeClassNames` from
  `@hali/design-system`; lastUpdatedAt formatted client-side.
- **v0 origin:** `institution:components/dashboard/overview.tsx:40–67`.

### `SignalCard` (list view)

Row-level signal representation for the Live Signals list.

- **Props:** `signal: SignalListItem`, `canPostUpdate: boolean`.
- **Events:** `onOpenDetail(signalId)`, `onPostUpdate(signalId)`.
- **Behaviour:** hover reveals inline actions; stopPropagation on
  action buttons so the card click still works elsewhere.
- **v0 origin:** `institution:components/dashboard/live-signals.tsx`
  (signal rendering).

### `ConditionPill`

Semantic status chip.

- **Props:** `condition: "Active" | "Elevated" | "Calm"`.
- **Behaviour:** uses the shared condition-badge palette in
  `@hali/design-system`. Always includes a text label alongside
  colour; never colour-only.
- **v0 origin:** inline in `live-signals.tsx` + `overview.tsx`.

### `TrendIndicator`

- **Props:** `trend: "Growing" | "Stable" | "Slowing" | "Possible restoration"`.
- **Behaviour:** arrow icon + label, colour matches direction. On
  "Possible restoration" uses emerald hue.
- **v0 origin:** inline in signal components.

### `ResponseStatusPill`

- **Props:** `status: ResponseStatus` (seven canonical values from
  v0 `types.ts`).
- **Behaviour:** neutral treatment except "Service restored" (emerald)
  and "Teams on site" (primary).
- **v0 origin:** inline in signal components.

---

## 3. List + table patterns

### `FilterChipRow`

- **Props:** `options: { id; label; count?: number }[]`,
  `selectedId: string`.
- **Events:** `onSelect(id)`.
- **Behaviour:** single-select. Chip showing count renders the
  count in a smaller font to the right of the label. "All" option
  is always first.
- **v0 origin:** Live Signals top-of-list (inferred).

### `SignalList`

- **Props:** `signals: SignalListItem[]`, `canPostUpdate: boolean`,
  `cursor: string | null`, `isLoadingMore: boolean`.
- **Events:** `onOpenDetail`, `onPostUpdate`, `onLoadMore`.
- **Behaviour:** infinite-scroll via cursor; shows the
  `calm empty state` when `signals.length === 0 && !isLoadingMore`.
- **v0 origin:** Live Signals page composition.

### `EmptyState`

- **Props:** `message: string`, `timestamp: string`, `icon?: LucideIcon`.
- **Behaviour:** neutral treatment, no illustration. Matches the
  citizen mobile "calm state" tone — institution is operational,
  not ambient, so copy is factual ("No active signals in scope.
  Last refreshed 2m ago.").

### `LoadingSkeleton`

- **Props:** `variant: "card" | "row" | "grid"`, `count?: number`.
- **Behaviour:** matches final content layout so layout shift is
  minimal when real content arrives.

---

## 4. Signal Detail composition

### `SignalDetailHeader`

- **Props:** `signal: SignalDetail`.
- **Slots:** `actions: ReactNode` — "Post update" + any stage-specific
  action CTAs.
- **Behaviour:** renders title, area, category badge, condition pill,
  trend, response status. Includes the
  `RestorationWaitingConfirmationBanner` when applicable (see §5).
- **v0 origin:** `institution:components/dashboard/signal-detail.tsx`.

### `SignalDescription`

- **Props:** `description: string`, `citizenSummary: string`.
- **Behaviour:** institution-authored description is shown above
  the citizen summary; the citizen summary is labelled "Citizen
  summary" so it is clearly distinguished.

### `SignalMetaGrid`

- **Props:** `affectedCount: number`, `recentReports24h: number`,
  `location: string`, `timeActive: string`.
- **Behaviour:** 2×2 grid of label/value pairs. Values use
  `FontSize.cardTitle` for prominence; labels use `FontSize.badge`
  muted-foreground.

### `OfficialUpdateTimeline`

- **Props:** `updates: OfficialUpdate[]`, `canPostUpdate: boolean`.
- **Events:** `onPostUpdate()`.
- **Behaviour:** chronological top→bottom with timestamp + stage
  chip + message + expected-resolution subtext. "Post update" CTA
  at the top; disabled when `canPostUpdate === false`.

---

## 5. Workflows

### 5.1 Official-update creation (`PostUpdateModal`)

- **Props:** `signal: SignalListItem`, `kindsAllowed: UpdateKind[]`.
- **Events:** `onSubmit(payload)`, `onDismiss()`.
- **Kinds:** `live_update`, `scheduled_disruption`,
  `advisory_public_notice` — the three canonical backend kinds.
- **Fields by kind:**
  - `live_update`: stage (response status), message, affected areas,
    expected resolution (optional).
  - `scheduled_disruption`: start + end time, affected areas,
    message, severity.
  - `advisory_public_notice`: topic, message, publish window.
- **Behaviour:** form validates client-side; server call is
  `POST /v1/official-posts`. On success, parent receives the new
  update and prepends it to the timeline optimistically.
- **v0 origin:** `institution:components/dashboard/post-update-modal.tsx`.
  The v0 post-update only models the stage flow; the other two kinds
  are a Phase 1.5 extension carried into Phase 3.

### 5.2 Restoration action (`RestorationActionCTA` + banner)

- **Props:** `signal: SignalDetail`.
- **Events:** `onClaimRestoration()`.
- **Behaviour:**
  - Shown only when the server indicates the signal is in a
    restoration-eligible state.
  - Clicking `onClaimRestoration` calls
    `POST /v1/clusters/{id}/restoration-response` (proposed —
    see `hali_institution_backend_contract_implications.md` §3)
    and transitions the cluster to `possible_restoration`.
  - The `RestorationWaitingConfirmationBanner` then renders at the
    top of the Signal Detail until the citizen confirmation
    threshold resolves or the window expires.
- **v0 origin:** partial — v0 surfaces response-stage progression
  but doesn't isolate the post-claim wait state.

### 5.3 Activity feed (`ActivityFeed` + `ActivityFeedItem`)

- **Props:** `items: ActivityItem[]`, `isLive: boolean`.
- **Events:** `onItemClick(signalId)`.
- **Behaviour:** virtualised when items > 50. Each item uses
  `ActivityFeedItem` (see §6.6 in layout spec). `isLive === true`
  shows a subtle "live" indicator (no pulsing animation in
  institution — calm, operational tone).

### 5.4 Notification centre (`NotificationCenterPopover`)

- **Props:** `notifications: Notification[]`,
  `unreadCount: number`.
- **Events:** `onMarkAllRead`, `onItemClick(notification)`.
- **Types:** `new_signal`, `growing`, `needs_attention`,
  `restoration`, `update_posted`, `restored`.
- **Behaviour:** popover anchored to the topbar bell. Item click
  navigates to Signal Detail when `signalId` is present.

---

## 6. Form primitives

Consumed from shadcn/ui but with institution-scoped conventions:

- `Input` — always includes a `label` prop (rendered as a form label
  above, not inside, for accessibility).
- `DropdownMenu` — used for institution + area pickers; never for
  navigation (navigation lives in the sidebar).
- `Popover` — used for notifications.
- `Button` — variants: `primary`, `secondary`, `ghost`, `destructive`.
  Destructive uses the converged destructive hue from the design
  system, never inline colour.

---

## 7. Accessibility + keyboard rules

Every interactive component must:

- Have an accessible name (visible label or `aria-label`).
- Support keyboard activation (`Enter` / `Space` for buttons, arrow
  keys for nav).
- Preserve focus on modal dismiss (return to the trigger).
- Announce state changes via `aria-live="polite"` for the activity
  feed and notification centre.

---

## 8. Consumed from `@hali/design-system`

Import these directly — never redeclare in the institution web app:

- `InstitutionColors.*`, `CitizenColors.*` (for mobile preview
  surfaces, not institution runtime)
- `ConditionBadgeClassNames`
- `Spacing`, `Radius`, `FontFamily`, `FontSize`, `FontWeight`,
  `LineHeight`, `LetterSpacing`

Tailwind config in the institution web app maps these tokens into
CSS custom properties; the pattern is documented in the
institution-web scaffold PR (Phase 3).

---

## 9. What is NOT defined here

- Concrete Tailwind class compositions (implementation detail — each
  component decides).
- Test strategy (lives in the institution web app's test plan).
- Data-loading libraries (React Query is the default; documented at
  the app level).
- Observability hooks — see `OBSERVABILITY_MODEL.md` §5.
