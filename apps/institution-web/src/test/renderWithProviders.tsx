import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, type RenderResult } from "@testing-library/react";
import type { ReactNode } from "react";
import { RouterProvider, createMemoryRouter } from "react-router-dom";
import { institutionRoutes } from "../router";
import { FeatureFlagsTestProvider } from "./featureFlagsTestProvider";

// Test utility that mirrors App.tsx — QueryClientProvider wrapping
// the institution router — but with memory-based routing and a fresh
// QueryClient so test state never leaks. Every call creates its own
// client so retries and error caches don't bleed between cases.
//
// Flags are stubbed-enabled by default via `FeatureFlagsTestProvider`
// so screen tests written before #205 keep working without pulling
// in the `/v1/feature-flags` endpoint; the real provider is exercised
// by its own tests.

export interface RenderOptions {
  readonly pathname: string;
  readonly flagOverride?: Readonly<Record<string, boolean>>;
}

export interface RenderResultWithClient extends RenderResult {
  readonly queryClient: QueryClient;
}

export function renderWithProviders({
  pathname,
  flagOverride,
}: RenderOptions): RenderResultWithClient {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        staleTime: 0,
      },
    },
  });

  const router = createMemoryRouter(institutionRoutes, { initialEntries: [pathname] });

  const result = render(
    <QueryClientProvider client={queryClient}>
      <FeatureFlagsTestProvider override={flagOverride}>
        <RouterProvider router={router} />
      </FeatureFlagsTestProvider>
    </QueryClientProvider>,
  );

  return Object.assign(result, { queryClient });
}

// Smaller wrapper for screens that don't need routing context — lets
// tests render a single screen with just the query and flag providers.
export function withQueryClient(children: ReactNode, client?: QueryClient): RenderResult {
  const queryClient =
    client ??
    new QueryClient({
      defaultOptions: {
        queries: { retry: false, staleTime: 0 },
      },
    });
  return render(
    <QueryClientProvider client={queryClient}>
      <FeatureFlagsTestProvider>{children}</FeatureFlagsTestProvider>
    </QueryClientProvider>,
  );
}
