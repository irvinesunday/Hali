import { TelemetryRouteNames, type TelemetryRouteName } from "../telemetry/events";

// Typed route handle for the institution shell. React Router's
// `useMatches()` returns handles as `unknown`; this helper narrows
// safely so surfaces don't reach for `any`. Each route supplies:
//   - `title` — topbar heading
//   - `telemetryName` — bounded route identifier emitted with
//     `dashboard.route.entered` so metrics tags stay low-cardinality
//     even though the path contains cluster ids.

export interface RouteHandle {
  readonly title: string;
  readonly telemetryName: TelemetryRouteName;
}

const ROUTE_NAME_VALUES = new Set<string>(Object.values(TelemetryRouteNames));

export function isRouteHandle(value: unknown): value is RouteHandle {
  if (typeof value !== "object" || value === null) return false;
  const candidate = value as { title?: unknown; telemetryName?: unknown };
  // Narrow `telemetryName` against the canonical bounded set — a raw
  // string would compile past the TS claim but leak high-cardinality
  // values (e.g. a pathname) into `route_name` at runtime.
  return (
    typeof candidate.title === "string" &&
    typeof candidate.telemetryName === "string" &&
    ROUTE_NAME_VALUES.has(candidate.telemetryName)
  );
}
