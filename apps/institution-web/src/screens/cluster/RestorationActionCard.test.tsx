import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ComponentProps } from "react";
import { afterEach, describe, expect, it } from "vitest";
import { errorResponse, jsonResponse, mockFetch, restoreFetch } from "../../test/mockFetch";
import { RestorationActionCard } from "./RestorationActionCard";

function renderCard(overrides?: Partial<ComponentProps<typeof RestorationActionCard>>) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const result = render(
    <QueryClientProvider client={queryClient}>
      <RestorationActionCard
        clusterId={overrides?.clusterId ?? "cluster-1"}
        clusterState={overrides?.clusterState ?? "active"}
        clusterCategory={overrides?.clusterCategory ?? "electricity"}
        restorationRatio={overrides?.restorationRatio ?? null}
        restorationYesVotes={overrides?.restorationYesVotes ?? null}
        restorationTotalVotes={overrides?.restorationTotalVotes ?? null}
        resolvedAt={overrides?.resolvedAt ?? null}
      />
    </QueryClientProvider>,
  );
  return { ...result, queryClient };
}

describe("RestorationActionCard", () => {
  afterEach(() => {
    restoreFetch();
  });

  it("renders nothing for an unconfirmed cluster", () => {
    const { container } = renderCard({ clusterState: "unconfirmed" });
    expect(container).toBeEmptyDOMElement();
  });

  it("shows a 'Mark as restored' CTA when the cluster is active", () => {
    renderCard({ clusterState: "active" });
    expect(screen.getByRole("button", { name: /mark as restored/i })).toBeInTheDocument();
  });

  it("renders the awaiting-confirmation banner with ratio and votes for possible_restoration", () => {
    renderCard({
      clusterState: "possible_restoration",
      restorationRatio: 0.67,
      restorationYesVotes: 4,
      restorationTotalVotes: 6,
    });
    expect(screen.getByText(/awaiting citizen confirmation/i)).toBeInTheDocument();
    expect(screen.getByText("67%")).toBeInTheDocument();
    // Yes votes = 4, total = 6.
    expect(screen.getByText("4")).toBeInTheDocument();
    expect(screen.getByText("6")).toBeInTheDocument();
  });

  it("falls back to em-dashes when restoration counts have not been populated yet", () => {
    renderCard({ clusterState: "possible_restoration" });
    expect(screen.getAllByText("—").length).toBeGreaterThanOrEqual(3);
    expect(screen.getByText(/no citizen responses yet/i)).toBeInTheDocument();
  });

  it("renders the resolved banner when the cluster is resolved", () => {
    renderCard({
      clusterState: "resolved",
      resolvedAt: "2026-04-18T09:00:00Z",
    });
    expect(screen.getByText(/service restored/i)).toBeInTheDocument();
    expect(screen.getByText(/citizen confirmations reached the threshold/i)).toBeInTheDocument();
  });

  it("submits a restoration claim via POST /v1/official-posts with isRestorationClaim true", async () => {
    const captured: { url?: string; init?: RequestInit } = {};
    mockFetch({
      "/v1/official-posts": (url, init) => {
        captured.url = url;
        captured.init = init;
        return jsonResponse(
          {
            id: "post-1",
            institutionId: "inst-1",
            type: "live_update",
            category: "electricity",
            title: "Service restored",
            body: "Power is back on the affected blocks.",
            startsAt: null,
            endsAt: null,
            status: "published",
            relatedClusterId: "cluster-1",
            isRestorationClaim: true,
            createdAt: "2026-04-18T09:00:00Z",
            responseStatus: "restoration_in_progress",
            severity: null,
          },
          201,
        );
      },
    });

    renderCard({ clusterState: "active" });

    await userEvent.click(screen.getByRole("button", { name: /mark as restored/i }));

    const dialog = await screen.findByRole("dialog");
    expect(dialog).toHaveTextContent(/claim restoration/i);

    await userEvent.type(screen.getByLabelText(/^body$/i), "Power is back on the affected blocks.");

    await userEvent.click(screen.getByRole("button", { name: /confirm restoration claim/i }));

    await waitFor(() => {
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    });

    expect(captured.init?.method).toBe("POST");
    const body = JSON.parse(String(captured.init?.body));
    expect(body).toMatchObject({
      type: "live_update",
      category: "electricity",
      title: "Service restored",
      body: "Power is back on the affected blocks.",
      relatedClusterId: "cluster-1",
      isRestorationClaim: true,
      responseStatus: "restoration_in_progress",
    });
  });

  it("surfaces a server error without closing the dialog", async () => {
    mockFetch({
      "/v1/official-posts": () => errorResponse(409, "cluster_not_active"),
    });

    renderCard({ clusterState: "active" });

    await userEvent.click(screen.getByRole("button", { name: /mark as restored/i }));
    await userEvent.type(screen.getByLabelText(/^body$/i), "Power is back.");
    await userEvent.click(screen.getByRole("button", { name: /confirm restoration claim/i }));

    await waitFor(() => {
      expect(screen.getByRole("alert")).toHaveTextContent(/backend rejected this restoration claim/i);
    });
    expect(screen.getByRole("dialog")).toBeInTheDocument();
  });

  it("keeps the dialog open when the body is blank", async () => {
    renderCard({ clusterState: "active" });

    await userEvent.click(screen.getByRole("button", { name: /mark as restored/i }));
    await userEvent.click(screen.getByRole("button", { name: /confirm restoration claim/i }));

    // `required` on the textarea makes the browser block submission
    // before handleSubmit runs — the dialog staying open is the
    // affordance the user sees.
    expect(screen.getByRole("dialog")).toBeInTheDocument();
  });
});
