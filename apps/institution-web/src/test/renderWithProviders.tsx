import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, type RenderResult } from "@testing-library/react";
import type { ReactNode } from "react";
import { RouterProvider, createMemoryRouter } from "react-router-dom";
import { institutionRoutes } from "../router";

// Test utility that mirrors App.tsx — QueryClientProvider wrapping
// the institution router — but with memory-based routing and a fresh
// QueryClient so test state never leaks. Every call creates its own
// client so retries and error caches don't bleed between cases.

export interface RenderOptions {
  readonly pathname: string;
}

export interface RenderResultWithClient extends RenderResult {
  readonly queryClient: QueryClient;
}

export function renderWithProviders({ pathname }: RenderOptions): RenderResultWithClient {
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
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );

  return Object.assign(result, { queryClient });
}

// Smaller wrapper for screens that don't need routing context — lets
// tests render a single screen with just the query provider.
export function withQueryClient(children: ReactNode, client?: QueryClient): RenderResult {
  const queryClient =
    client ??
    new QueryClient({
      defaultOptions: {
        queries: { retry: false, staleTime: 0 },
      },
    });
  return render(<QueryClientProvider client={queryClient}>{children}</QueryClientProvider>);
}
