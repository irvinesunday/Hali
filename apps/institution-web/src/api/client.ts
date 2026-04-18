import { emitEvent } from "../telemetry/emit";
import { TelemetryEvents } from "../telemetry/events";

// Thin fetch wrapper for institution backend routes. Uses
// `credentials: "include"` so the forthcoming httpOnly session cookie
// (#254) is sent automatically. CSRF double-submit header wiring also
// lands in #254 — this file is the single place that will need to
// attach the x-csrf-token header once the cookie exists.
//
// The client throws `ApiError` for non-2xx responses. Individual
// screens catch via TanStack Query's `error` state and render a
// surface-level error view; the RootErrorBoundary is a last resort
// for non-query failures.
//
// 403 responses additionally emit `auth.scope.denied` — the
// observability model (§5.1) calls out scope-boundary hits as both a
// UX signal ("why did my action fail") and a security signal
// (repeated hits = misconfigured scope or hostile actor). We tag with
// a bucketed `route_template` (method + path with parameters
// collapsed) so metric cardinality stays bounded even when many
// cluster ids hit the same endpoint.

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "";

export class ApiError extends Error {
  readonly status: number;
  readonly url: string;

  constructor(status: number, url: string, message: string) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.url = url;
  }
}

// Collapse UUID segments so tag cardinality stays bounded —
// "/v1/institution/signals/<uuid>" becomes
// "/v1/institution/signals/:id". UUIDs are the only path-parameter
// shape Phase 2 routes emit today; if a numeric id or slug-only
// parameter ever lands on an institution route, widen this matcher
// (and its test coverage) at the same time. This is the bounded
// route-template rule in OBSERVABILITY_MODEL.md §7.2.
const UUID_SEGMENT =
  /\/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi;

function routeTemplateFor(path: string): string {
  const pathOnly = path.split("?")[0] ?? path;
  return pathOnly.replace(UUID_SEGMENT, "/:id");
}

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const url = `${API_BASE_URL}${path}`;
  const response = await fetch(url, {
    ...init,
    credentials: "include",
    headers: {
      Accept: "application/json",
      ...(init?.headers ?? {}),
    },
  });

  if (!response.ok) {
    if (response.status === 403) {
      emitEvent(TelemetryEvents.AuthScopeDenied, {
        route_template: routeTemplateFor(path),
        method: (init?.method ?? "GET").toUpperCase(),
      });
    }
    throw new ApiError(response.status, url, `Request failed (${response.status})`);
  }

  return (await response.json()) as T;
}
