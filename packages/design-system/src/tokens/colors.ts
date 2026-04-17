// Canonical color tokens, extracted from the v0 reference artifacts
// under docs/reference-ui/v0/. WEB-ONLY — the citizen mobile app
// maintains its own React Native theme in
// apps/citizen-mobile/src/theme/colors.ts because OKLCH and semantic
// CSS tokens are not representable in React Native.
//
// Architecture (per Phase 1.5 design intent clarification and
// subsequent human review gate):
//
//   SharedSemanticColors  — structural chrome + shared meaning,
//                            unified across surfaces.
//   CitizenColors         — surface override; warmer, softer,
//                            ambient register.
//   InstitutionColors     — surface override; cooler, sharper,
//                            operational register.
//
// Shared semantic base carries meaning that is identical across
// surfaces (what "card" means, what "border" means). Where a token
// is semantically shared but rendered differently per surface — e.g.
// `destructive` carries the same "error/danger" meaning but the
// citizen surface renders it softer and the institution surface
// renders it sharper — the token lives in the surface override
// layers, not in the shared base. This is the "shared semantic base
// with surface-level rendering differences (not fully unified)"
// rule.

/**
 * Shared base: structural chrome that every surface consumes
 * identically. Divergent v0 values for these tokens were minor
 * chrome drift and have been reconciled per the classification rule
 * (see `docs/reference-ui/v0/phase-1.5-visual-audit.md` §8).
 */
export const SharedSemanticColors = {
  card: "oklch(1 0 0)",
  cardForeground: "oklch(0.225 0.02 210)",
  popover: "oklch(1 0 0)",
  popoverForeground: "oklch(0.225 0.02 210)",
  border: "oklch(0.91 0.01 190)",
  input: "oklch(0.92 0.01 190)",
  muted: "oklch(0.955 0.007 190)",
  mutedForeground: "oklch(0.5 0.02 210)",
} as const;

/**
 * Citizen surface override — warmer, softer, empathetic register.
 * Source: `docs/reference-ui/v0/v0-hali-mobile-ui.zip:app/globals.css`.
 *
 * Intentional divergences from InstitutionColors:
 *   primary      — cooler hue-190 teal at lightness 0.55 (softer feel)
 *   foreground   — slightly cooler body text
 *   destructive  — warmer, less saturated error hue (matches the
 *                  surface's empathetic register while preserving
 *                  the shared "error/danger" semantic)
 */
export const CitizenColors = {
  background: "oklch(0.98 0.005 200)",
  foreground: "oklch(0.25 0.02 220)",
  primary: "oklch(0.55 0.12 190)",
  primaryForeground: "oklch(0.99 0 0)",
  secondary: "oklch(0.94 0.01 200)",
  secondaryForeground: "oklch(0.35 0.02 220)",
  accent: "oklch(0.92 0.02 180)",
  accentForeground: "oklch(0.30 0.02 220)",
  destructive: "oklch(0.60 0.15 30)",
  destructiveForeground: "oklch(0.99 0 0)",
  ring: "oklch(0.55 0.12 190)",
} as const;

/**
 * Institution surface override — cooler, sharper, decisive register.
 * Source: `docs/reference-ui/v0/v0-hali-institution-ui.zip:app/globals.css`.
 *
 * Intentional divergences from CitizenColors:
 *   primary      — hue-180 teal at lightness 0.65 (sharper,
 *                  authoritative)
 *   foreground   — darker, less blue-hued body text for denser
 *                  dashboard content
 *   destructive  — more saturated, sharper error hue (matches the
 *                  operational register; same shared "error/danger"
 *                  semantic as citizen)
 *
 * Sidebar tokens apply only to the institution dashboard shell and
 * have no citizen counterpart.
 */
export const InstitutionColors = {
  background: "oklch(0.985 0.002 180)",
  foreground: "oklch(0.2 0.02 200)",
  primary: "oklch(0.65 0.12 180)",
  primaryForeground: "oklch(0.99 0 0)",
  secondary: "oklch(0.96 0.01 180)",
  secondaryForeground: "oklch(0.25 0.02 200)",
  accent: "oklch(0.92 0.03 180)",
  accentForeground: "oklch(0.25 0.02 200)",
  destructive: "oklch(0.577 0.245 27.325)",
  destructiveForeground: "oklch(0.99 0 0)",
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

/**
 * Helpers that assemble a complete token set for a given surface by
 * layering a surface override on top of the shared base. Downstream
 * consumers typically import one of these rather than mixing base +
 * override manually. Exported PascalCase to match the rest of the
 * token surface; lowercase aliases are kept for back-compat.
 */
export const CitizenTheme = {
  ...SharedSemanticColors,
  ...CitizenColors,
} as const;

export const InstitutionTheme = {
  ...SharedSemanticColors,
  ...InstitutionColors,
} as const;

export const citizenTheme = CitizenTheme;
export const institutionTheme = InstitutionTheme;
