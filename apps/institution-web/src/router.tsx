import type { RouteObject } from "react-router-dom";
import { createBrowserRouter } from "react-router-dom";
import { InstitutionShell } from "./shell/InstitutionShell";
import { OverviewScreen } from "./screens/OverviewScreen";
import { SignalsScreen } from "./screens/SignalsScreen";
import { ClusterDetailScreen } from "./screens/ClusterDetailScreen";
import { AreasScreen } from "./screens/AreasScreen";
import { MetricsScreen } from "./screens/MetricsScreen";
import { NotFoundScreen } from "./screens/NotFoundScreen";

// Institution dashboard route tree. Every authenticated surface
// renders inside InstitutionShell; unauthenticated routes (login,
// verify-totp) land in #254 and slot in as peers of the shell route
// with their own layouts.
//
// Each route declares its topbar title via `handle: { title }` so the
// shell can derive the heading from the active match rather than
// hard-mapping paths in Sidebar order (which would mislabel 404s).
//
// Exported as a standalone array so tests can mount it via
// createMemoryRouter with a chosen initialEntry.
export const institutionRoutes: RouteObject[] = [
  {
    path: "/",
    element: <InstitutionShell />,
    children: [
      { index: true, element: <OverviewScreen />, handle: { title: "Overview" } },
      { path: "signals", element: <SignalsScreen />, handle: { title: "Live Signals" } },
      {
        path: "signals/:clusterId",
        element: <ClusterDetailScreen />,
        handle: { title: "Signal detail" },
      },
      { path: "areas", element: <AreasScreen />, handle: { title: "Areas" } },
      { path: "metrics", element: <MetricsScreen />, handle: { title: "Metrics" } },
      { path: "*", element: <NotFoundScreen />, handle: { title: "Page not found" } },
    ],
  },
];

export const router: ReturnType<typeof createBrowserRouter> = createBrowserRouter(
  institutionRoutes,
  {
    future: {
      v7_relativeSplatPath: true,
      v7_fetcherPersist: true,
      v7_normalizeFormMethod: true,
      v7_partialHydration: true,
      v7_skipActionErrorRevalidation: true,
    },
  },
);
