// Top-level navigation model for the institution dashboard. Single
// source of truth: the sidebar component renders from this list and
// the router registers the same routes, so the two can't drift.
//
// Surface implementations (overview metrics, live signals feed,
// areas/jurisdictions view, aggregate metrics) land in #202. This
// module only carries the label, path, and section key so the shell
// has a coherent navigation scaffold to wire against.

export interface NavItem {
  readonly key: string;
  readonly label: string;
  readonly path: string;
}

export const primaryNavigation: readonly NavItem[] = [
  { key: "overview", label: "Overview", path: "/" },
  { key: "signals", label: "Live Signals", path: "/signals" },
  { key: "areas", label: "Areas", path: "/areas" },
  { key: "metrics", label: "Metrics", path: "/metrics" },
] as const;
