// Typed route handle for the institution shell. React Router's
// `useMatches()` returns handles as `unknown`; this helper narrows
// safely so surfaces don't reach for `any`. Each route that wants
// its own topbar title supplies `handle: { title: "…" }`.

export interface RouteHandle {
  readonly title: string;
}

export function isRouteHandle(value: unknown): value is RouteHandle {
  return (
    typeof value === "object" &&
    value !== null &&
    "title" in value &&
    typeof (value as { title: unknown }).title === "string"
  );
}
