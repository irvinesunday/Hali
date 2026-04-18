import { QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "react-router-dom";
import { DashboardGate } from "./featureFlags/DashboardGate";
import { FeatureFlagsProvider } from "./featureFlags/FeatureFlagsProvider";
import { queryClient } from "./query/client";
import { router } from "./router";

// Application entrypoint. Mounts the TanStack Query cache provider so
// every screen shares one client, then the feature-flags provider
// and master dashboard gate around the router. The gate resolves
// `institution_web.enabled` before anything authenticated renders,
// so a rollback-flipped flag blanks the shell without a redeploy.
//
// Auth gates (redirect to /login, step-up prompts) land in #254 as a
// wrapper inside each route element, sitting below the flag gate so
// login screens are also subject to the master rollout.
export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <FeatureFlagsProvider>
        <DashboardGate>
          <RouterProvider router={router} />
        </DashboardGate>
      </FeatureFlagsProvider>
    </QueryClientProvider>
  );
}
