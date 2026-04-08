/**
 * Hali design token — border radius scale.
 * Base radius: 12dp (--radius: 0.75rem equivalent from web MVP).
 */

export const Radius = {
  /** 4dp — tight, for inner elements */
  xs: 4,
  /** 8dp */
  sm: 8,
  /** 10dp */
  md: 10,
  /** 12dp — standard card radius */
  lg: 12,
  /** 16dp — large cards, modals */
  xl: 16,
  /** 24dp — sheets, large containers */
  '2xl': 24,
  /** 9999dp — pill shapes, badges, FAB */
  full: 9999,
} as const;
