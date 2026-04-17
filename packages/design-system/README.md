# @hali/design-system

Shared Hali design system tokens and primitives derived from the v0
reference artifacts in `docs/reference-ui/v0/`.

**Web-only.** Never import this package from `apps/citizen-mobile`.
Mobile ships its own React Native theme in
`apps/citizen-mobile/src/theme/` because React Native style primitives
and web CSS are not interchangeable. The rule is stated explicitly in
`CLAUDE.md` under "Stack rules": _"Do NOT import /packages/design-system
into citizen-mobile"_.

## Architecture: shared base + surface overrides

Per Phase 1.5 design intent clarification, divergent surface colours
are **intentional theming**, not conflicts to unify. Tokens are
organised into three layers:

1. **`SharedSemanticColors`** — structural chrome (border, muted,
   input, card, popover) and their foreground pairs. Unified across
   surfaces.
2. **`CitizenColors`** — surface override for the citizen mobile
   preview / future citizen web. Warmer, softer, empathetic register.
   Carries surface-specific `background`, `foreground`, `primary`,
   `secondary`, `accent`, `destructive`, `ring`, plus their
   foreground pairs.
3. **`InstitutionColors`** — surface override for the institution
   dashboard. Cooler, sharper, decisive register. Same key set as
   `CitizenColors` plus the sidebar-specific tokens.

The package also exports pre-composed themes (PascalCase with
back-compat lowercase aliases):

- `CitizenTheme = { ...SharedSemanticColors, ...CitizenColors }`
- `InstitutionTheme = { ...SharedSemanticColors, ...InstitutionColors }`

Downstream consumers typically import the composed theme rather than
mixing layers manually. `destructive` lives on the surface overrides
(not in the shared base) so each surface renders it with a hue
that matches its emotional register while retaining the same
semantic meaning.

### Why the divergence is modelled this way

| Token | Citizen | Institution | Rationale |
|---|---|---|---|
| `primary` | `oklch(0.55 0.12 190)` — warmer, softer | `oklch(0.65 0.12 180)` — cooler, sharper | Intentional surface divergence per product doctrine |
| `foreground` | `oklch(0.25 0.02 220)` | `oklch(0.2 0.02 200)` — darker | Institution surface is denser and needs higher-contrast body text |
| `border`, `muted`, `input`, `card` | shared | shared | Structural chrome — no emotional register difference |
| `destructive` | `oklch(0.60 0.15 30)` — warmer, softer | `oklch(0.577 0.245 27.325)` — sharper, more saturated | Shared **semantic** meaning (error / danger) with surface-specific rendering. Each surface keeps its v0 hue; `destructiveForeground` is converged to near-white for legibility on both. |
| `sidebar*` | n/a | defined | Only the institution shell has a persistent sidebar |

Full rationale: `docs/arch/hali_institution_ux_layout_spec.md` and
`docs/reference-ui/v0/phase-1.5-visual-audit.md` §8.

## Canonical vs reference-only

| Layer | Status |
|---|---|
| **Color tokens** (shared base + surface overrides) | **Canonical** — extracted from the v0 artifacts and reconciled per the classification rule |
| **Condition badge class names** (`ConditionBadgeClassNames`) | **Canonical** — mirrors the v0 mobile `issue-card.tsx` mapping, which is the authoritative source for condition → palette mapping |
| **Spacing scale** | **Canonical** — 4px base, xs..6xl steps |
| **Radius scale** | **Canonical default** (`0.75rem`), derived sm/md per shadcn/ui convention |
| **Typography tokens** (Geist family, weights, scale, line-heights) | **Canonical** — Geist matches both v0 surfaces |
| **Component primitives** (buttons, cards, shells) | **Reference-only** — the v0 artifacts ship concrete components but they are NOT imported into this package. Phase 1.5 synthesis defines the institution patterns (`docs/arch/hali_institution_component_patterns.md`); Phase 3 institution-web implementation ships them. |

## Usage

```ts
import {
  SharedSemanticColors,
  CitizenColors,
  InstitutionColors,
  CitizenTheme,
  InstitutionTheme,
  ConditionBadgeClassNames,
  Spacing,
  Radius,
  FontFamily,
  FontSize,
  FontWeight,
  DesignSystemVersion,
} from "@hali/design-system";

// Institution web consumer — use the composed theme
const theme = InstitutionTheme;

// Or layer manually for a one-off override
const custom = {
  ...SharedSemanticColors,
  ...InstitutionColors,
  primary: "custom-value",
};
```

`citizenTheme` and `institutionTheme` (camelCase) remain exported as
aliases for back-compat but the PascalCase names match the rest of
the public surface.

Consumers feed these into Tailwind v4 (via CSS custom properties
generated at build time) or CSS-in-JS. The package exports raw values
(OKLCH strings, rem strings, numeric weights) — the consumer picks
the consumption strategy. The package does not ship a Tailwind
config of its own; that lands with the first web app build.

## What lives here vs not

- ✅ Token values extracted verbatim from the v0 reference artifacts.
- ✅ Cross-surface variants (citizen vs institution) when the surface
  register is deliberately different.
- ✅ Shared structural + semantic tokens where both surfaces have
  the same meaning.
- ❌ Concrete React components — Phase 3 ships those.
- ❌ Tailwind config — one config per app, importing from this package.
- ❌ React Native primitives — mobile ships its own theme.

## See also

- `docs/arch/hali_institution_ux_layout_spec.md` — institution
  dashboard UX + layout (where tokens are applied).
- `docs/arch/hali_institution_component_patterns.md` — reusable
  component patterns that consume these tokens.
- `docs/reference-ui/v0/phase-1.5-visual-audit.md` — citations and
  classification of every cross-surface difference.
- `docs/reference-ui/v0/mobile-token-visual-audit.md` — retroactive
  audit of the citizen mobile theme.
- `docs/arch/SECURITY_POSTURE.md` §6 — secure defaults for new web apps.
- `docs/arch/OBSERVABILITY_MODEL.md` §5 — institution web telemetry.
