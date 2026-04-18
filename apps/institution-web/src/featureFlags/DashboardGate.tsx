import type { ReactNode } from "react";
import {
  InstitutionWebFlagKeys,
  useFeatureFlag,
  useFeatureFlagsStatus,
} from "./FeatureFlagsProvider";

// Master shell gate driven by `institution_web.enabled`. Wraps the
// whole router so a flipped flag blanks every authenticated surface
// — including the topbar, sidebar, and screens — without requiring
// a redeploy.
//
// While the flag fetch is in flight we render a minimal loading
// surface; rendering the real shell and then pulling it back would
// let a user click into a feature that's about to vanish. If the
// fetch fails we fall through to the "disabled" surface, matching
// the conservative default in `FeatureFlagsProvider`.
//
// The "dashboard-gate-status" data attribute is the integration
// hook used by tests and observability probes to distinguish the
// three states without colour-matching the copy.

export interface DashboardGateProps {
  readonly children: ReactNode;
}

export function DashboardGate({ children }: DashboardGateProps) {
  const { isLoading } = useFeatureFlagsStatus();
  const enabled = useFeatureFlag(InstitutionWebFlagKeys.enabled);

  if (isLoading) {
    return <GateLoading />;
  }

  if (!enabled) {
    return <GateDisabled />;
  }

  return <>{children}</>;
}

function GateLoading() {
  return (
    <div
      data-testid="dashboard-gate-status"
      data-state="loading"
      className="flex min-h-screen items-center justify-center bg-background text-sm text-muted-foreground"
      role="status"
      aria-live="polite"
    >
      Resolving dashboard access…
    </div>
  );
}

function GateDisabled() {
  return (
    <div
      data-testid="dashboard-gate-status"
      data-state="disabled"
      className="flex min-h-screen items-center justify-center bg-background px-6"
      role="alert"
    >
      <div className="max-w-md space-y-3 text-center">
        <h1 className="text-xl font-semibold text-foreground">
          The institution dashboard is currently unavailable.
        </h1>
        <p className="text-sm text-muted-foreground">
          Access to this surface is controlled by a rollout flag. If you believe you should have
          access, contact your Hali Ops partner — no action is required from your side.
        </p>
      </div>
    </div>
  );
}
