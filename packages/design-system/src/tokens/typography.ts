// Typography tokens. Both v0 artifacts load the Geist family from
// Google Fonts in their Next.js layout. The scale below is derived
// from the Tailwind classes used in the v0 components — appName /
// title / cardTitle / body / badge / sectionHeader / micro — and
// aligned with the citizen mobile scale
// (apps/citizen-mobile/src/theme/typography.ts).

export const FontFamily = {
  sans: "Geist, 'Geist Fallback', system-ui, sans-serif",
  mono: "'Geist Mono', 'Geist Mono Fallback', ui-monospace, monospace",
} as const;

export const FontWeight = {
  regular: 400,
  medium: 500,
  semiBold: 600,
  bold: 700,
} as const;

export const FontSize = {
  /** 24px — app name ("Hali" header) */
  appName: "1.5rem",
  /** 20px — screen/modal titles */
  title: "1.25rem",
  /** 15px — card titles, prominent labels */
  cardTitle: "0.9375rem",
  /** 14px — body text */
  body: "0.875rem",
  /** 13px — secondary body (institution names, context) */
  bodySmall: "0.8125rem",
  /** 12px — condition badges, meta counts */
  badge: "0.75rem",
  /** 10px — section headers (uppercase), micro labels */
  micro: "0.625rem",
} as const;

export const LineHeight = {
  tight: 1.2,
  snug: 1.35,
  normal: 1.5,
} as const;

export const LetterSpacing = {
  /** Default body text */
  normal: "0",
  /** Section header uppercase labels */
  wide: "0.05em",
} as const;
