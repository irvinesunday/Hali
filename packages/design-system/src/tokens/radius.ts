// Border radius scale. The canonical v0 value is --radius: 0.75rem
// (12px), used directly on cards, FAB, and modal surfaces as
// `rounded-xl`. Smaller steps follow the shadcn/ui convention:
//   sm = --radius - 4px  (8px, 0.5rem)
//   md = --radius - 2px  (10px, 0.625rem)
// Consumers that need a tighter 4px chip corner should reach for an
// explicit pixel value rather than a new token key.

export const Radius = {
  /** 8px — sm (0.75rem - 4px per shadcn/ui convention) */
  sm: "0.5rem",
  /** 10px — md (0.75rem - 2px per shadcn/ui convention) */
  md: "0.625rem",
  /** 12px — default; matches v0 globals.css --radius */
  default: "0.75rem",
  /** 16px — lg */
  lg: "1rem",
  /** 9999px — full (pills, avatars) */
  full: "9999px",
} as const;

export type RadiusKey = keyof typeof Radius;
