import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { afterEach, describe, expect, it } from "vitest";
import { errorResponse, jsonResponse, mockFetch, restoreFetch } from "../test/mockFetch";
import {
  FeatureFlagsProvider,
  InstitutionWebFlagKeys,
  useFeatureFlag,
  useFeatureFlagsStatus,
} from "./FeatureFlagsProvider";

function renderWithClient(children: ReactNode) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: 0 } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <FeatureFlagsProvider>{children}</FeatureFlagsProvider>
    </QueryClientProvider>,
  );
}

function FlagProbe({ flagKey }: { readonly flagKey: string }) {
  const enabled = useFeatureFlag(flagKey as never);
  const { isLoading, isError } = useFeatureFlagsStatus();
  return (
    <div>
      <span data-testid="loading">{isLoading ? "yes" : "no"}</span>
      <span data-testid="error">{isError ? "yes" : "no"}</span>
      <span data-testid="flag">{enabled ? "on" : "off"}</span>
    </div>
  );
}

describe("FeatureFlagsProvider", () => {
  afterEach(() => {
    restoreFetch();
  });

  it("exposes resolved flags from /v1/feature-flags", async () => {
    mockFetch({
      "/v1/feature-flags": () =>
        jsonResponse({
          flags: {
            [InstitutionWebFlagKeys.enabled]: true,
            [InstitutionWebFlagKeys.postUpdateEnabled]: false,
          },
        }),
    });

    renderWithClient(<FlagProbe flagKey={InstitutionWebFlagKeys.enabled} />);

    await waitFor(() => {
      expect(screen.getByTestId("loading")).toHaveTextContent("no");
    });
    expect(screen.getByTestId("flag")).toHaveTextContent("on");
    expect(screen.getByTestId("error")).toHaveTextContent("no");
  });

  it("returns false for keys missing from the resolved payload", async () => {
    mockFetch({
      "/v1/feature-flags": () => jsonResponse({ flags: {} }),
    });

    renderWithClient(<FlagProbe flagKey={InstitutionWebFlagKeys.postUpdateEnabled} />);

    await waitFor(() => {
      expect(screen.getByTestId("loading")).toHaveTextContent("no");
    });
    expect(screen.getByTestId("flag")).toHaveTextContent("off");
  });

  it("safe-disables every flag when the fetch fails", async () => {
    mockFetch({
      "/v1/feature-flags": () => errorResponse(500),
    });

    renderWithClient(<FlagProbe flagKey={InstitutionWebFlagKeys.enabled} />);

    // The provider retries once before surfacing an error — allow
    // enough time for the retry + error-state settle. The per-test
    // timeout is the default 5s, so 4s gives comfortable headroom.
    await waitFor(
      () => {
        expect(screen.getByTestId("error")).toHaveTextContent("yes");
      },
      { timeout: 4_000 },
    );
    expect(screen.getByTestId("flag")).toHaveTextContent("off");
  });
});
