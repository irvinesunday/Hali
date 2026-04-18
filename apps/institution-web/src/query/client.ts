import { QueryClient } from "@tanstack/react-query";

// QueryClient factory. Tests mount a fresh client per render so
// cache state cannot leak between test cases; the app mounts a
// singleton via `src/App.tsx`. Defaults favour dashboard freshness
// over cache aggressiveness: a 30-second stale window keeps the
// operator view close to realtime without flooding the API on every
// navigation back to Overview.
export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: 1,
        staleTime: 30_000,
        refetchOnWindowFocus: false,
      },
    },
  });
}

export const queryClient = createQueryClient();
