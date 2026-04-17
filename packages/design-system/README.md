# @hali/design-system

Shared Hali design system tokens and primitives derived from the v0
reference artifacts in `docs/reference-ui/v0/`.

**Web-only.** Never import this package from `apps/citizen-mobile`.
Mobile ships its own React Native theme in
`apps/citizen-mobile/src/theme/` because React Native style primitives
and web CSS are not interchangeable. The rule is stated explicitly in
`CLAUDE.md` under "Stack rules": _"Do NOT import /packages/design-system
into citizen-mobile"_.

## Canonical vs reference-only

| Layer | Status |
|---|---|
| **Color tokens** (`CitizenColors`, `InstitutionColors`) | **Canonical** — extracted from `docs/reference-ui/v0/v0-hali-*-ui.zip` `app/globals.css`. The two surfaces differ; see §5 of `docs/reference-ui/v0/mobile-token-visual-audit.md` for the cross-surface flags. |
| **Condition badge class names** (`ConditionBadgeClassNames`) | **Canonical** — mirrors the explicit `issue-card.tsx` mapping in the v0 mobile UI, which is the authoritative source for condition → palette mapping. |
| **Spacing scale** | **Canonical** — aligned with the mobile theme (4px base, xs..6xl steps). |
| **Radius scale** | **Canonical default** (`0.75rem`), derived steps for `sm`/`md` per shadcn/ui convention. |
| **Typography tokens** (Geist family, weights, sizes, line-heights) | **Canonical** — Geist matches both v0 surfaces; scale aligned with citizen mobile. |
| **Component primitives** (buttons, inputs, cards, shells) | **Reference-only** — v0 artifacts ship concrete components but they are NOT imported into this package. Phase 1.5 synthesis decides which patterns become canonical; Phase 3 implementation ships them. |

## Usage

```ts
import {
  CitizenColors,
  InstitutionColors,
  ConditionBadgeClassNames,
  Spacing,
  Radius,
  FontFamily,
  FontSize,
  DesignSystemVersion,
} from "@hali/design-system";
```

Consumers typically feed these into Tailwind v4 (via CSS custom
properties generated at build time) or CSS-in-JS at runtime. The
package exports raw values (OKLCH strings, rem strings, numeric
weights) so the consumer picks the consumption strategy — the
package does not ship a Tailwind config of its own yet; that lands
with the first web app build.

## What lives here vs not

- ✅ Token values extracted verbatim from the v0 reference artifacts.
- ✅ Cross-surface variants (citizen vs institution) when the v0 surfaces
  differ.
- ✅ Shared constants that both surfaces consume identically
  (condition-badge palette, font family, radius default).
- ❌ Concrete React components (ship with each web app, or land after
  Phase 1.5 decides on the canonical component set).
- ❌ Tailwind config (one Tailwind config per app — the shared tokens
  are imported into each app's config rather than centralised here).
- ❌ React Native primitives (mobile ships its own theme).

## See also

- `docs/reference-ui/v0/mobile-token-visual-audit.md` — retroactive
  audit of the citizen mobile theme against the v0 artifacts.
- `docs/arch/SECURITY_POSTURE.md` §6 — secure defaults for new web apps.
- `docs/arch/OBSERVABILITY_MODEL.md` §5 — institution web telemetry.
