import { vi } from "vitest";

// Fetch mocking helpers. Screens call `fetch` via `apiFetch`, so we
// stub `globalThis.fetch` per test with a route table. Unknown URLs
// throw so a test can't accidentally pass against a real network
// call. Always call `restoreFetch()` in `afterEach` to keep tests
// isolated — it delegates to Vitest's `unstubAllGlobals`, which
// restores the platform's original `fetch` (Node 22+ ships a native
// one) rather than leaving `undefined` behind.

type RouteHandler = (url: string, init?: RequestInit) => Response | Promise<Response>;

export interface RouteTable {
  readonly [path: string]: RouteHandler;
}

type FetchLike = (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;

export function mockFetch(routes: RouteTable): void {
  const fetchMock = vi.fn<FetchLike>(async (input, init) => {
    const url = typeof input === "string" ? input : input.toString();
    for (const [path, handler] of Object.entries(routes)) {
      if (url.endsWith(path) || url.includes(`${path}?`)) {
        return handler(url, init);
      }
    }
    throw new Error(`Unmocked fetch: ${url}`);
  });
  vi.stubGlobal("fetch", fetchMock);
}

export function jsonResponse<T>(body: T, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

export function errorResponse(status: number, message = "Server error"): Response {
  return new Response(JSON.stringify({ message }), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

export function restoreFetch(): void {
  vi.unstubAllGlobals();
}
