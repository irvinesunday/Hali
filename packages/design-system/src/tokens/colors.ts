// Canonical color tokens extracted from the v0 reference artifacts
// (docs/reference-ui/v0/v0-hali-*-ui.zip). These are WEB-ONLY — the
// citizen mobile app maintains its own React Native theme in
// apps/citizen-mobile/src/theme/colors.ts because OKLCH and
// semantic CSS tokens are not representable in React Native.
//
// The two v0 surfaces differ subtly — the citizen mobile reference
// uses a cooler primary (oklch 0.55 0.12 190) while the institution
// reference uses a slightly more saturated variant (oklch 0.65 0.12
// 180). Both are captured here so each web consumer picks the
// surface-correct palette; convergence decisions are deferred to the
// Phase 1.5 UI/UX synthesis work.

/**
 * Canonical citizen web palette — matches the v0 mobile UI artifact
 * (`v0-hali-mobile-ui/app/globals.css`). Values are raw OKLCH strings
 * so downstream consumers can feed them directly into Tailwind v4 or
 * CSS custom properties without lossy hex conversion.
 */
export const CitizenColors = {
  background: "oklch(0.98 0.005 200)",
  foreground: "oklch(0.25 0.02 220)",
  card: "oklch(1 0 0)",
  cardForeground: "oklch(0.25 0.02 220)",
  popover: "oklch(1 0 0)",
  popoverForeground: "oklch(0.25 0.02 220)",
  primary: "oklch(0.55 0.12 190)",
  primaryForeground: "oklch(0.99 0 0)",
  secondary: "oklch(0.94 0.01 200)",
  secondaryForeground: "oklch(0.35 0.02 220)",
  muted: "oklch(0.95 0.005 200)",
  mutedForeground: "oklch(0.50 0.02 220)",
  accent: "oklch(0.92 0.02 180)",
  accentForeground: "oklch(0.30 0.02 220)",
  destructive: "oklch(0.60 0.15 30)",
  destructiveForeground: "oklch(0.60 0.15 30)",
  border: "oklch(0.90 0.01 200)",
  input: "oklch(0.92 0.01 200)",
  ring: "oklch(0.55 0.12 190)",
} as const;

/**
 * Canonical institution web palette — matches the v0 institution UI
 * artifact (`v0-hali-institution-ui/app/globals.css`). Sidebar-prefixed
 * tokens are included because the institution dashboard shell has a
 * persistent sidebar while the citizen mobile reference does not.
 */
export const InstitutionColors = {
  background: "oklch(0.985 0.002 180)",
  foreground: "oklch(0.2 0.02 200)",
  card: "oklch(1 0 0)",
  cardForeground: "oklch(0.2 0.02 200)",
  popover: "oklch(1 0 0)",
  popoverForeground: "oklch(0.2 0.02 200)",
  primary: "oklch(0.65 0.12 180)",
  primaryForeground: "oklch(0.99 0 0)",
  secondary: "oklch(0.96 0.01 180)",
  secondaryForeground: "oklch(0.25 0.02 200)",
  muted: "oklch(0.96 0.008 180)",
  mutedForeground: "oklch(0.5 0.02 200)",
  accent: "oklch(0.92 0.03 180)",
  accentForeground: "oklch(0.25 0.02 200)",
  destructive: "oklch(0.577 0.245 27.325)",
  destructiveForeground: "oklch(0.577 0.245 27.325)",
  border: "oklch(0.92 0.01 180)",
  input: "oklch(0.92 0.01 180)",
  ring: "oklch(0.65 0.12 180)",
  sidebar: "oklch(0.99 0.005 180)",
  sidebarForeground: "oklch(0.25 0.02 200)",
  sidebarPrimary: "oklch(0.65 0.12 180)",
  sidebarPrimaryForeground: "oklch(0.99 0 0)",
  sidebarAccent: "oklch(0.96 0.01 180)",
  sidebarAccentForeground: "oklch(0.25 0.02 200)",
  sidebarBorder: "oklch(0.92 0.01 180)",
  sidebarRing: "oklch(0.65 0.12 180)",
} as const;

/**
 * Semantic condition-badge palette shared across surfaces. Each entry
 * is a trio of (background, text, border) Tailwind class names taken
 * directly from the v0 mobile `issue-card.tsx` mapping, which is the
 * authoritative source. The citizen mobile app mirrors these palettes
 * as hex tokens in `apps/citizen-mobile/src/theme/colors.ts`.
 *
 * When adding a new condition label to CSI-NLP, extend the mapping in
 * the citizen mobile `getConditionBadgePalette` helper rather than
 * introducing a new palette key here.
 */
export const ConditionBadgeClassNames = {
  amber: "bg-amber-50 text-amber-700 border-amber-200",
  orange: "bg-orange-50 text-orange-700 border-orange-200",
  red: "bg-red-50 text-red-700 border-red-200",
  sky: "bg-sky-50 text-sky-700 border-sky-200",
  violet: "bg-violet-50 text-violet-700 border-violet-200",
  stone: "bg-stone-50 text-stone-700 border-stone-200",
  slate: "bg-slate-50 text-slate-700 border-slate-200",
  yellow: "bg-yellow-50 text-yellow-700 border-yellow-200",
  emerald: "bg-emerald-50 text-emerald-700 border-emerald-200",
  muted: "bg-muted text-muted-foreground border-border",
} as const;

export type ConditionBadgeKey = keyof typeof ConditionBadgeClassNames;
