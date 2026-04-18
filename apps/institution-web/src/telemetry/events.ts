// Canonical event-name catalog for the institution dashboard. Every
// `emitEvent` call site references one of these constants — never a
// raw string — so renaming a live event is a single-line change here
// plus a Grafana/dashboard rule update, and CI can statically catch
// a surface that invents its own event.
//
// Names follow §6 of `docs/arch/OBSERVABILITY_MODEL.md`:
//   - dot-delimited lowercase snake_case segments
//   - subject → verb → outcome (e.g. `overview.load.completed`)
//   - outcomes limited to: started, completed, failed, submitted,
//     cancelled, denied, entered, rendered
//   - values (category, reason_code) are tags, not embedded in the name
//
// Events that belong to the auth flow (`auth.login.web`, etc.) will be
// added with #254. This file covers only what institution-web MVP
// surfaces today — shell navigation, the three core screen loads, the
// two write flows, and scope-denied responses.

export const TelemetryEvents = {
  DashboardRouteEntered: "dashboard.route.entered",
  OverviewLoadStarted: "overview.load.started",
  OverviewLoadCompleted: "overview.load.completed",
  OverviewLoadFailed: "overview.load.failed",
  SignalsBoardLoadStarted: "signals.board.load.started",
  SignalsBoardLoadCompleted: "signals.board.load.completed",
  SignalsBoardLoadFailed: "signals.board.load.failed",
  ClusterDetailLoadStarted: "cluster.detail.load.started",
  ClusterDetailLoadCompleted: "cluster.detail.load.completed",
  ClusterDetailLoadFailed: "cluster.detail.load.failed",
  OfficialPostDraftStarted: "official_post.draft.started",
  OfficialPostCreateSubmitted: "official_post.create.submitted",
  OfficialPostCreateCompleted: "official_post.create.completed",
  OfficialPostCreateFailed: "official_post.create.failed",
  OfficialPostDraftCancelled: "official_post.draft.cancelled",
  RestorationClaimStarted: "restoration.claim.started",
  RestorationClaimSubmitted: "restoration.claim.submitted",
  RestorationClaimCompleted: "restoration.claim.completed",
  RestorationClaimFailed: "restoration.claim.failed",
  RestorationClaimCancelled: "restoration.claim.cancelled",
  AuthScopeDenied: "auth.scope.denied",
} as const;

export type TelemetryEventName = (typeof TelemetryEvents)[keyof typeof TelemetryEvents];

// Bounded route-name catalog used as a tag value on
// `dashboard.route.entered`. Adding a new route means adding a name
// here and referencing it from that route's handle — never free-text
// from `location.pathname`, which would embed cluster ids in metrics.
export const TelemetryRouteNames = {
  Overview: "overview",
  Signals: "signals",
  ClusterDetail: "cluster_detail",
  Areas: "areas",
  Metrics: "metrics",
  NotFound: "not_found",
} as const;

export type TelemetryRouteName =
  (typeof TelemetryRouteNames)[keyof typeof TelemetryRouteNames];
