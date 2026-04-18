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
    throw new ApiError(response.status, url, `Request failed (${response.status})`);
  }

  return (await response.json()) as T;
}
