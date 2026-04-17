# Mobile Token Visual Audit — Retroactive

**Issue:** #189. **Generated:** 2026-04-17. **Purpose:** internal accuracy
record, not a design decision.

The Phase A design tokens in `apps/citizen-mobile/src/theme/` were
extracted from the web MVP's `globals.css` rather than from a direct
visual inspection of specific v0 mobile screens. This document is the
retroactive audit: for each token group, it declares the source, cites
the v0 screen(s) that validate or contradict the value, flags the
assumptions that have no direct visual evidence, and calls out the
cross-surface risks for Phase B.

There is no human review gate on this audit (per the Phase 1 brief) —
it is an accuracy ledger that exists so Phase B does not rediscover
the same gaps.

---

## 1. Extraction source declaration

| Token group | Source used for Phase A extraction | Direct v0 screen evidence? |
|---|---|---|
| Colors — surface (background, card, foreground, muted, border) | `v0-hali-mobile-ui/app/globals.css` OKLCH values, hex-converted | **Yes** — every mobile screen renders against these surface tokens |
| Colors — primary brand (teal) | `v0-hali-mobile-ui/app/globals.css` `--primary` | **Yes** — home-screen header, issue-card active state, FAB background |
| Colors — destructive (warm orange/red) | `globals.css` `--destructive` | **Partial** — error paths not consistently rendered in v0 components |
| Colors — condition badges (amber/orange/red/sky/violet/stone/slate/yellow/emerald) | `v0-hali-mobile-ui/components/issue-card.tsx` explicit class map (authoritative) | **Yes** — issue-card.tsx maps condition labels to Tailwind palette classes directly |
| Typography — font family (Geist) | `v0-hali-mobile-ui/app/layout.tsx` (`Geist`, `Geist_Mono` imports) | **Yes** — mobile layout loads Geist from Google Fonts |
| Typography — scale (FontSize.*) | Tailwind classes used across v0 components (`text-2xl`, `text-sm`, etc.) translated to dp | **Inferred** — scale mapping is a judgment call; no pixel measurement taken from a rendered v0 screen |
| Spacing — scale (4dp base, xs..6xl) | Tailwind default 4px base matches RN 4dp convention | **Inferred** — not directly measured |
| Radius — scale | `globals.css` `--radius: 0.75rem` | **Yes** — v0 uses `rounded-xl` (12px) on cards, FAB, modal surfaces |
| Shadows | Tailwind defaults | **Partial** — some v0 components rely on opacity-based separation rather than shadow |
| Animations (pulse, fade-up, count-pop, breathe, slide-in-right) | Web MVP keyframe set | **Inferred** — animation timing not directly observable from static component source |

---

## 2. Screen-level citations

### Colors

- **`Colors.primary` (#2D8A8F / `oklch(0.55 0.12 190)`)** — Confirmed across:
  - `home-screen.tsx`: `text-primary` on the locality pin icon, the
    clickable locality label, and the "All clear" emerald affirmative
    subtext color is contrasted against it.
  - `issue-card.tsx`: uses it for active state accents.
  - `floating-action-button.tsx`: FAB background.
  - `locality-selector.tsx`: selected locality chip.
  - **Verdict:** confirmed across 4+ screens. Safe to treat as canonical.

- **`Colors.background` (#F7FAFA / `oklch(0.98 0.005 200)`)** — Confirmed
  on the app root shell in `home-screen.tsx` (`bg-background`). Every
  screen composes on top of this surface. **Safe.**

- **`Colors.foreground` (#383D42 / `oklch(0.25 0.02 220)`)** — Used as
  the default text color on `home-screen.tsx`, `issue-detail.tsx`, and
  the app-name header. **Safe.**

- **`Colors.mutedForeground` (#757B82 / `oklch(0.50 0.02 220)`)** — Used
  for secondary text across `home-screen.tsx` (subtext under locality
  name, "Active Now" section label) and in empty-state copy. **Safe.**

- **`Colors.border` (#E2E7E7 / `oklch(0.90 0.01 200)`)** — Used on
  header bottom-border (`border-border/50`) and section dividers
  (`border-t border-border/30`). **Safe.**

- **`Colors.faintForeground` (#9CA3AF)** — No direct v0 mobile evidence;
  the mobile theme introduces this as a finer-grained variant between
  `muted-foreground` and the backgrounds. **[ASSUMPTION — no direct
  v0 evidence]** — the v0 often uses opacity modifiers
  (`text-muted-foreground/70`) rather than a separate token. Phase B
  should either adopt opacity-modifier convention on mobile or validate
  that `faintForeground` reads correctly against real content.

- **`Colors.destructive` (#D4603A)** — OKLCH to hex conversion. No
  direct error-state v0 screen exists to validate the hue. **[ASSUMPTION
  — no direct v0 evidence]**. Phase B will exercise this when error
  envelopes render in real screens.

- **`Colors.emerald` / `Colors.emeraldSubtle`** — Confirmed in
  `home-screen.tsx` "All clear" empty state (`bg-emerald-50`,
  `text-emerald-600`). Palette is the Tailwind `emerald-*` set; hex
  values in the mobile theme match Tailwind. **Safe.**

### Typography

- **Font family (Geist)** — Confirmed loaded in mobile v0
  `app/layout.tsx`. The citizen mobile app loads it via
  `@expo-google-fonts/geist`. **Safe.**

- **`FontSize.appName` (24dp / `text-2xl`)** — Confirmed in
  `home-screen.tsx` header (`text-2xl font-semibold`). **Safe.**

- **`FontSize.title` (20dp / `text-xl`)** — Used in
  `issue-detail.tsx`, `official-update-detail.tsx`, and
  `report-modal.tsx` for screen titles. **Safe.**

- **`FontSize.cardTitle` (15dp)** — **[ASSUMPTION — no direct v0 evidence]**.
  v0 cards use `text-sm` (14dp) or `text-base` (16dp). 15dp was a
  judgment call midway between the two. Phase B risk: cards may read
  either slightly smaller or slightly larger than the v0 intent; check
  when issue-card is rendered with real content.

- **`FontSize.body` (14dp / `text-sm`)** — Confirmed in
  `home-screen.tsx` "All clear in {locality}" copy. **Safe.**

- **`FontSize.bodySmall` (13dp)** — **[ASSUMPTION — no direct v0 evidence]**.
  v0 uses either `text-sm` (14dp) or `text-xs` (12dp); 13dp is
  interpolated. Phase B risk: institution name labels and timestamp
  subtext may render at slightly different sizes than the v0.

- **`FontSize.badge` (12dp / `text-xs`)** — Confirmed in v0 condition
  badges and meta text. **Safe.**

- **`FontSize.sectionHeader` / `FontSize.micro` (both 10dp)** —
  Confirmed in `home-screen.tsx` "Active Now" (`text-xs`, slightly
  smaller custom) and "Official Updates" (`text-[10px]`). **Safe.**

### Spacing

- **`Spacing.lg` (16dp) = default screen padding** — Confirmed via
  v0 Tailwind `p-4` on `home-screen.tsx` scroll container. **Safe.**

- **`Spacing.2xl` (24dp) = section gap** — Confirmed via v0
  `space-y-6` on home feed sections. **Safe.**

- **Other scale steps (xs, sm, md, xl, 3xl, 4xl, 6xl)** — mobile theme
  mirrors the Tailwind 4-unit scale. Where specific v0 usage is
  unclear, the token is labeled **[ASSUMPTION — no direct v0 evidence]**.
  Phase B risk: tight scales (xs, sm) may drift slightly from v0 intent.

### Radius

- **`Radius` default 0.75rem (12dp, `rounded-xl`)** — Confirmed across
  v0 cards, buttons, FAB, and modal surfaces. **Safe.**

- **Smaller radii (sm, md)** — **[ASSUMPTION — derived from shadcn/ui
  convention]**. Not separately measured against v0 artifacts.

### Shadows

- **All shadow tokens** — **[ASSUMPTION — no direct v0 evidence]**. The
  v0 mobile reference frequently relies on opacity modifiers
  (`bg-background/95`, `border-border/50`, `backdrop-blur-sm`) rather
  than explicit shadows. Phase B should audit whether mobile should
  switch to opacity-based separation or keep discrete shadow tokens.

### Animations

- **Animation presets (fade-up, count-pop, breathe, slide-in-right,
  modal-content)** — **[ASSUMPTION — timing not measurable from static
  component source]**. Values were ported from the web MVP keyframes
  as intent-preserving approximations. Phase B should review
  `apps/citizen-mobile/src/theme/animations.ts` against a running
  reference if animation polish is in scope.
  - `startPulseSoft` / `pulseSoftConfig` have been removed in #187
    after LiveDot was deleted — those were the only consumers.

---

## 3. Assumption flags summary

| Token | Status | Phase B action |
|---|---|---|
| `Colors.faintForeground` | No direct v0 evidence | Validate against real screens or switch to opacity modifiers |
| `Colors.destructive` exact hue | No direct v0 error screens | Check against error envelopes when they render |
| `FontSize.cardTitle` (15dp) | Interpolated between 14 and 16 | Measure against real card rendering |
| `FontSize.bodySmall` (13dp) | Interpolated between 12 and 14 | Measure against institution-name labels |
| Spacing scale steps except lg/2xl | Derived from Tailwind default | Sanity-check during Phase B layout work |
| Radius `sm` / `md` | Derived from shadcn/ui convention | Validate on native pill / chip components |
| Shadow tokens (all) | No direct v0 shadow usage observed | Decide: opacity-based vs shadow tokens |
| Animation timing | Ported from web MVP, not measured | Visual review when motion polish is in scope |

---

## 4. Condition badge colour map

The `getConditionBadgePalette()` mapping in
`apps/citizen-mobile/src/theme/colors.ts` is string-match based. For
each palette key, its v0 evidence:

| Palette key | v0 evidence |
|---|---|
| `amber` — "No power", "Power outage", "No water", "Transformer", "Sewage", "Bad smell" | **Confirmed** — `v0-hali-mobile-ui/components/issue-card.tsx` maps these condition labels to `bg-amber-50 text-amber-700 border-amber-200` |
| `orange` — "Difficult to pass", "Multiple potholes", "Lane blocked", "Slow moving", "Heavy traffic", "Poor road condition", "Road damage", "Partially blocked", "Sidewalk/walkway", "Uncollected", "Overflowing" | **Confirmed** — same `issue-card.tsx` map |
| `red` — "Impassable", "Road blocked", "Traffic blocked", "Accident", "Crash", "Collision" | **Confirmed** — same map |
| `sky` — "Flooding", "Water on road", "Partially flooded" | **Confirmed** — same map |
| `violet` — "Noise", "Noisy", "Loud" | **Confirmed** — same map (v0 covers "Loud music", "Noisy construction") |
| `stone` — "Dust", "Dusty" | **Confirmed** — same map |
| `slate` — "Dark", "No street lighting", "No light" | **Confirmed** — same map ("Dark streets", "No street lighting") |
| `yellow` — "Low pressure", "Weak pressure", "Unstable power", "Power going on/off", "Air pollution" | **Confirmed** — same map |
| `emerald` — "Restoration", "Possible restoration", "Restored", "Recovery" | **Judgment call** — v0 `issue-card.tsx` uses emerald for the "Restored / possible restoration" cluster state; extending it to "recovery" free-text was the theme's choice |
| `muted` — fallback | **Canonical** — safety net for unknown condition labels; not a v0 artifact decision |

The mobile theme's string-matching helper is an order-sensitive match
over a regex/substring list. Ordering (e.g. `amber` before `orange`)
was chosen to keep "No power" from matching the `amber` clause before
a more specific clause could claim it. Phase B adding a new condition
must also add a new clause in the same ordered helper.

---

## 5. Cross-surface flags

The most important divergences between mobile v0 and institution v0
tokens that Phase 1.5 / Phase B will need to reconcile:

| Token | Mobile v0 | Institution v0 | Resolution needed? |
|---|---|---|---|
| Primary color | `oklch(0.55 0.12 190)` — cooler, deeper teal | `oklch(0.65 0.12 180)` — lighter, slightly greener teal | **Yes** — Phase 1.5 should decide whether brand primary converges or deliberately stays surface-specific |
| Background | `oklch(0.98 0.005 200)` | `oklch(0.985 0.002 180)` — very slightly warmer | Minor; probably acceptable |
| Foreground | `oklch(0.25 0.02 220)` | `oklch(0.2 0.02 200)` — darker, less blue-hued | **Yes** — worth an explicit decision for shared body text |
| Border | `oklch(0.90 0.01 200)` | `oklch(0.92 0.01 180)` — slightly lighter | Minor |
| Destructive | `oklch(0.60 0.15 30)` — warm orange-red | `oklch(0.577 0.245 27.325)` — more saturated, closer to pure red | **Yes** — error states should feel consistent |
| Sidebar tokens | Not applicable (no sidebar on mobile) | Full sidebar-* palette defined | Institution web is the only consumer; no conflict |
| Radius | `0.75rem` (both) | `0.75rem` (both) | **No difference** |
| Typography (Geist family) | Confirmed | Confirmed | **No difference** |

These cross-surface flags are inputs to the Phase 1.5 UI/UX synthesis;
the final decisions live in that synthesis document, not here.

---

## 6. Phase B risk summary

Tokens most likely to need adjustment once real screens are rendered
against them:

1. **`Colors.faintForeground`** — no direct v0 evidence; may be
   replaced with opacity modifiers on existing `mutedForeground`
   rather than kept as a discrete token.
2. **`FontSize.cardTitle` (15dp)** — interpolated; likely drifts up to
   16dp or down to 14dp once cards render with real content.
3. **`FontSize.bodySmall` (13dp)** — same interpolation concern.
4. **Shadow tokens** — the v0 reference favours opacity-based
   separation, so the mobile shadow set may be simplified or removed.
5. **Animation timing** — values are intent-preserving approximations;
   actual perceived motion may need adjustment.
6. **Cross-surface primary/destructive** — decision pending from
   Phase 1.5; mobile tokens may need a follow-up update if Phase 1.5
   chooses convergence.

None of these block Phase B from starting; they are a known watchlist
for when Phase B encounters the relevant surfaces.

---

## 7. Audit outcome

- **Number of "safe" tokens:** 13 (every surface color, primary,
  foreground, muted-foreground, border, emerald subset, page-level
  radius, body/title/badge/appName/micro font sizes, Geist family,
  default screen spacing)
- **Number of "assumption-flagged" tokens:** 8 (faintForeground,
  destructive hue, cardTitle, bodySmall, most spacing scale steps,
  smaller radii, shadow set, animation timing)
- **Cross-surface conflicts requiring Phase 1.5 decisions:** 4
  (primary color, foreground color, destructive hue, sidebar usage)

The assumption count is manageable and the cross-surface conflicts are
all narrow. No blocking issue; Phase B can proceed on the current
mobile theme with this document as the change log for follow-ups.
