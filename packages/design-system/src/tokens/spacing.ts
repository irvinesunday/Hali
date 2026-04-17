// Spacing scale — base unit 4px, aligned with the citizen mobile
// theme (apps/citizen-mobile/src/theme/spacing.ts). Values are raw
// rem strings so consumers can feed them into Tailwind, CSS
// custom properties, or CSS-in-JS without arithmetic per consumer.

export const Spacing = {
  /** 4px — xs */
  xs: "0.25rem",
  /** 8px — sm */
  sm: "0.5rem",
  /** 12px — md */
  md: "0.75rem",
  /** 16px — lg, the default screen/card padding */
  lg: "1rem",
  /** 20px — xl */
  xl: "1.25rem",
  /** 24px — 2xl, standard section gap */
  "2xl": "1.5rem",
  /** 32px — 3xl */
  "3xl": "2rem",
  /** 48px — 4xl */
  "4xl": "3rem",
  /** 96px — 6xl */
  "6xl": "6rem",
} as const;

export type SpacingKey = keyof typeof Spacing;
