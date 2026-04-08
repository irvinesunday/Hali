/**
 * Hali design token — spacing scale.
 * Base unit: 4dp. All values are multiples of 4.
 */

export const Spacing = {
  /** 4dp */
  xs: 4,
  /** 8dp */
  sm: 8,
  /** 12dp */
  md: 12,
  /** 16dp — standard screen/card padding (p-4 equivalent) */
  lg: 16,
  /** 20dp */
  xl: 20,
  /** 24dp — section gap (space-y-6 equivalent) */
  '2xl': 24,
  /** 32dp */
  '3xl': 32,
  /** 48dp */
  '4xl': 48,
  /** 96dp */
  '6xl': 96,
} as const;

/** Horizontal padding for all full-width screen content */
export const ScreenPaddingH = Spacing.lg;

/** Vertical padding at bottom of scrollable screens (above nav bar) */
export const ScreenPaddingBottom = Spacing['6xl'];
