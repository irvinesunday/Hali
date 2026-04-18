import { createBrowserRouter } from "react-router-dom";
import { InstitutionShell } from "./shell/InstitutionShell";
import { OverviewScreen } from "./screens/OverviewScreen";
import { SignalsScreen } from "./screens/SignalsScreen";
import { AreasScreen } from "./screens/AreasScreen";
import { MetricsScreen } from "./screens/MetricsScreen";
import { NotFoundScreen } from "./screens/NotFoundScreen";

// Institution dashboard route tree. Every authenticated surface
// renders inside InstitutionShell; unauthenticated routes (login,
// verify-totp) land in #254 and slot in as peers of the shell route
// with their own layouts.
export const router: ReturnType<typeof createBrowserRouter> = createBrowserRouter(
  [
    {
      path: "/",
      element: <InstitutionShell />,
      children: [
        { index: true, element: <OverviewScreen /> },
        { path: "signals", element: <SignalsScreen /> },
        { path: "areas", element: <AreasScreen /> },
        { path: "metrics", element: <MetricsScreen /> },
        { path: "*", element: <NotFoundScreen /> },
      ],
    },
  ],
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
