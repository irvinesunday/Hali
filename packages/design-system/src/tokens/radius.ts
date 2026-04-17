// Border radius scale. The canonical v0 value is --radius: 0.75rem
// (`rounded-xl`). Smaller values are derived by the shadcn/ui
// convention (sm = radius - 4px, md = radius - 2px).

export const Radius = {
  /** 4px — sm */
  sm: "0.25rem",
  /** 6px — md */
  md: "0.375rem",
  /** 12px — default; matches v0 globals.css --radius */
  default: "0.75rem",
  /** 16px — lg */
  lg: "1rem",
  /** 9999px — full (pills, avatars) */
  full: "9999px",
} as const;

export type RadiusKey = keyof typeof Radius;
