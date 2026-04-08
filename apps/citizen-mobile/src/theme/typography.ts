/**
 * Hali design token — typography scale.
 *
 * Font: Geist (sans-serif) loaded via @expo-google-fonts/geist.
 * Scale derived from the web MVP's Tailwind classes.
 * FontSize values are in dp (density-independent pixels).
 * LineHeightMultiplier values are ratios — multiply by FontSize to get an
 * absolute dp value for React Native's TextStyle.lineHeight:
 *   lineHeight: FontSize.body * LineHeightMultiplier.normal  // → 21
 */

export const FontFamily = {
  regular: 'Geist_400Regular',
  medium: 'Geist_500Medium',
  semiBold: 'Geist_600SemiBold',
  bold: 'Geist_700Bold',
} as const;

export const FontSize = {
  /** App name "Hali" header */
  appName: 24,
  /** Screen/modal titles */
  title: 20,
  /** Card titles, prominent labels */
  cardTitle: 15,
  /** Body text, descriptions */
  body: 14,
  /** Secondary body — institution names, context */
  bodySmall: 13,
  /** Condition badges, meta counts */
  badge: 12,
  /** Section headers (uppercase) */
  sectionHeader: 10,
  /** Micro labels — timestamps, updated-at */
  micro: 10,
} as const;

/** Ratio multipliers — use as: lineHeight = FontSize.* * LineHeightMultiplier.* */
export const LineHeightMultiplier = {
  tight: 1.2,
  snug: 1.35,
  normal: 1.5,
} as const;

export const LetterSpacing = {
  /** For section header uppercase labels */
  wide: 0.8,
  /** Default */
  normal: 0,
} as const;
