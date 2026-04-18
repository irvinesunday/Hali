import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ComponentProps } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  resetTelemetryTransport,
  setTelemetryTransport,
  type TelemetryEventRecord,
} from "../../telemetry/emit";
import { TelemetryEvents } from "../../telemetry/events";
import { FeatureFlagsTestProvider } from "../../test/featureFlagsTestProvider";
import { errorResponse, jsonResponse, mockFetch, restoreFetch } from "../../test/mockFetch";
import { PostUpdateModal } from "./PostUpdateModal";

function renderModal(overrides?: Partial<ComponentProps<typeof PostUpdateModal>>) {
  const onClose = overrides?.onClose ?? vi.fn();
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const result = render(
    <QueryClientProvider client={queryClient}>
      <FeatureFlagsTestProvider>
        <PostUpdateModal
          clusterId={overrides?.clusterId ?? "cluster-1"}
          clusterCategory={overrides?.clusterCategory ?? "electricity"}
          open={overrides?.open ?? true}
          onClose={onClose}
        />
      </FeatureFlagsTestProvider>
    </QueryClientProvider>,
  );
  return { ...result, onClose, queryClient };
}

describe("PostUpdateModal", () => {
  afterEach(() => {
    restoreFetch();
  });

  it("is hidden when open is false", () => {
    renderModal({ open: false });
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("submits a live_update request with the chosen response status", async () => {
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
            title: "Teams dispatched",
            body: "Crews are en route.",
            startsAt: null,
            endsAt: null,
            status: "published",
            relatedClusterId: "cluster-1",
            isRestorationClaim: false,
            createdAt: "2026-04-18T04:00:00Z",
            responseStatus: "teams_dispatched",
            severity: null,
          },
          201,
        );
      },
    });

    const { onClose } = renderModal();

    await userEvent.type(screen.getByLabelText(/^title$/i), "Teams dispatched");
    await userEvent.type(screen.getByLabelText(/^body$/i), "Crews are en route.");
    await userEvent.selectOptions(
      screen.getByLabelText(/response status/i),
      "teams_dispatched",
    );

    await userEvent.click(screen.getByRole("button", { name: /post update/i }));

    await waitFor(() => expect(onClose).toHaveBeenCalledTimes(1));

    expect(captured.init?.method).toBe("POST");
    const body = JSON.parse(String(captured.init?.body));
    expect(body).toMatchObject({
      type: "live_update",
      category: "electricity",
      title: "Teams dispatched",
      body: "Crews are en route.",
      relatedClusterId: "cluster-1",
      responseStatus: "teams_dispatched",
    });
    expect(body).not.toHaveProperty("severity");
  });

  it("reveals severity + schedule fields when scheduled_disruption is picked", async () => {
    const captured: { init?: RequestInit } = {};
    mockFetch({
      "/v1/official-posts": (_url, init) => {
        captured.init = init;
        return jsonResponse(
          {
            id: "post-2",
            institutionId: "inst-1",
            type: "scheduled_disruption",
            category: "water",
            title: "Scheduled water maintenance",
            body: "Water supply will be paused for maintenance.",
            startsAt: "2026-04-19T09:00:00.000Z",
            endsAt: "2026-04-19T11:00:00.000Z",
            status: "published",
            relatedClusterId: "cluster-1",
            isRestorationClaim: false,
            createdAt: "2026-04-18T04:00:00Z",
            responseStatus: null,
            severity: "moderate",
          },
          201,
        );
      },
    });

    renderModal({ clusterCategory: "water" });

    await userEvent.click(screen.getByRole("radio", { name: /scheduled disruption/i }));
    await userEvent.type(screen.getByLabelText(/^title$/i), "Scheduled water maintenance");
    await userEvent.type(
      screen.getByLabelText(/^body$/i),
      "Water supply will be paused for maintenance.",
    );
    await userEvent.selectOptions(screen.getByLabelText(/severity/i), "moderate");
    // datetime-local values use the local timezone; we just assert they serialize as ISO.
    fireInputEvent(screen.getByLabelText(/^starts$/i), "2026-04-19T09:00");
    fireInputEvent(screen.getByLabelText(/^ends$/i), "2026-04-19T11:00");

    await userEvent.click(screen.getByRole("button", { name: /post update/i }));

    await waitFor(() => expect(captured.init?.body).toBeTruthy());
    const body = JSON.parse(String(captured.init?.body));
    expect(body).toMatchObject({
      type: "scheduled_disruption",
      category: "water",
      severity: "moderate",
    });
    expect(body.startsAt).toMatch(/^2026-04-19T/);
    expect(body.endsAt).toMatch(/^2026-04-19T/);
    expect(body).not.toHaveProperty("responseStatus");
  });

  it("omits response status and severity for an advisory post", async () => {
    const captured: { init?: RequestInit } = {};
    mockFetch({
      "/v1/official-posts": (_url, init) => {
        captured.init = init;
        return jsonResponse(
          {
            id: "post-3",
            institutionId: "inst-1",
            type: "advisory_public_notice",
            category: "environment",
            title: "Advisory notice",
            body: "General public notice.",
            startsAt: null,
            endsAt: null,
            status: "published",
            relatedClusterId: "cluster-1",
            isRestorationClaim: false,
            createdAt: "2026-04-18T04:00:00Z",
            responseStatus: null,
            severity: null,
          },
          201,
        );
      },
    });

    renderModal({ clusterCategory: "environment" });

    await userEvent.click(
      screen.getByRole("radio", { name: /advisory \/ public notice/i }),
    );
    await userEvent.type(screen.getByLabelText(/^title$/i), "Advisory notice");
    await userEvent.type(screen.getByLabelText(/^body$/i), "General public notice.");

    expect(screen.queryByLabelText(/response status/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/severity/i)).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: /post update/i }));

    await waitFor(() => expect(captured.init?.body).toBeTruthy());
    const body = JSON.parse(String(captured.init?.body));
    expect(body).not.toHaveProperty("responseStatus");
    expect(body).not.toHaveProperty("severity");
    expect(body).not.toHaveProperty("startsAt");
  });

  it("surfaces a server error without closing the modal", async () => {
    mockFetch({
      "/v1/official-posts": () => errorResponse(400, "invalid_category"),
    });

    const { onClose } = renderModal();

    await userEvent.type(screen.getByLabelText(/^title$/i), "Teams dispatched");
    await userEvent.type(screen.getByLabelText(/^body$/i), "Crews are en route.");
    await userEvent.click(screen.getByRole("button", { name: /post update/i }));

    await waitFor(() => {
      expect(screen.getByRole("alert")).toHaveTextContent(/backend rejected this update/i);
    });
    expect(onClose).not.toHaveBeenCalled();
  });

  it("blocks submit when title or body are blank", async () => {
    renderModal();

    await userEvent.click(screen.getByRole("button", { name: /post update/i }));

    expect(screen.getByRole("dialog")).toBeInTheDocument();
  });

  describe("telemetry", () => {
    let transport: ReturnType<typeof vi.fn>;

    beforeEach(() => {
      transport = vi.fn();
      setTelemetryTransport(transport);
    });

    afterEach(() => {
      resetTelemetryTransport();
    });

    function names(): string[] {
      return transport.mock.calls.map((c) => (c[0] as TelemetryEventRecord).name);
    }

    function recordOf(name: string): TelemetryEventRecord | undefined {
      return transport.mock.calls
        .map((c) => c[0] as TelemetryEventRecord)
        .find((r) => r.name === name);
    }

    it("emits draft.started on open with the cluster category tag", () => {
      renderModal({ clusterCategory: "water" });
      const started = recordOf(TelemetryEvents.OfficialPostDraftStarted);
      expect(started).toBeDefined();
      expect(started?.tags).toMatchObject({ cluster_category: "water" });
    });

    it("emits create.submitted then create.completed on a successful post", async () => {
      mockFetch({
        "/v1/official-posts": () =>
          jsonResponse(
            {
              id: "post-1",
              institutionId: "inst-1",
              type: "live_update",
              category: "electricity",
              title: "Teams dispatched",
              body: "Crews are en route.",
              startsAt: null,
              endsAt: null,
              status: "published",
              relatedClusterId: "cluster-1",
              isRestorationClaim: false,
              createdAt: "2026-04-18T04:00:00Z",
              responseStatus: null,
              severity: null,
            },
            201,
          ),
      });

      renderModal();

      await userEvent.type(screen.getByLabelText(/^title$/i), "Teams dispatched");
      await userEvent.type(screen.getByLabelText(/^body$/i), "Crews are en route.");
      await userEvent.click(screen.getByRole("button", { name: /post update/i }));

      await waitFor(() => {
        expect(names()).toContain(TelemetryEvents.OfficialPostCreateCompleted);
      });

      const sequence = names().filter((n) => n.startsWith("official_post."));
      expect(sequence).toEqual([
        TelemetryEvents.OfficialPostDraftStarted,
        TelemetryEvents.OfficialPostCreateSubmitted,
        TelemetryEvents.OfficialPostCreateCompleted,
      ]);
      expect(recordOf(TelemetryEvents.OfficialPostCreateSubmitted)?.tags).toMatchObject({
        post_type: "live_update",
      });
    });

    it("emits create.failed with the API status on a 4xx response", async () => {
      mockFetch({
        "/v1/official-posts": () => errorResponse(400, "invalid_category"),
      });

      renderModal();

      await userEvent.type(screen.getByLabelText(/^title$/i), "Teams dispatched");
      await userEvent.type(screen.getByLabelText(/^body$/i), "Crews are en route.");
      await userEvent.click(screen.getByRole("button", { name: /post update/i }));

      await waitFor(() => {
        expect(names()).toContain(TelemetryEvents.OfficialPostCreateFailed);
      });
      expect(recordOf(TelemetryEvents.OfficialPostCreateFailed)?.tags).toMatchObject({
        post_type: "live_update",
        status: 400,
      });
    });

    it("emits draft.cancelled when the user closes without submitting", async () => {
      const onClose = vi.fn();
      renderModal({ onClose });

      await userEvent.click(screen.getByRole("button", { name: /^cancel$/i }));

      expect(names()).toContain(TelemetryEvents.OfficialPostDraftCancelled);
      expect(onClose).toHaveBeenCalledTimes(1);
    });

    it("does not emit draft.cancelled after a successful post", async () => {
      mockFetch({
        "/v1/official-posts": () =>
          jsonResponse(
            {
              id: "post-1",
              institutionId: "inst-1",
              type: "live_update",
              category: "electricity",
              title: "Teams dispatched",
              body: "Crews are en route.",
              startsAt: null,
              endsAt: null,
              status: "published",
              relatedClusterId: "cluster-1",
              isRestorationClaim: false,
              createdAt: "2026-04-18T04:00:00Z",
              responseStatus: null,
              severity: null,
            },
            201,
          ),
      });

      renderModal();
      await userEvent.type(screen.getByLabelText(/^title$/i), "Teams dispatched");
      await userEvent.type(screen.getByLabelText(/^body$/i), "Crews are en route.");
      await userEvent.click(screen.getByRole("button", { name: /post update/i }));

      await waitFor(() => {
        expect(names()).toContain(TelemetryEvents.OfficialPostCreateCompleted);
      });
      expect(names()).not.toContain(TelemetryEvents.OfficialPostDraftCancelled);
    });
  });
});

// React Testing Library's userEvent doesn't reliably fire input events
// for the datetime-local control in jsdom — we emit a native input event
// so React's controlled component picks up the value.
function fireInputEvent(element: HTMLElement, value: string): void {
  const input = element as HTMLInputElement;
  const setter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, "value")?.set;
  setter?.call(input, value);
  input.dispatchEvent(new Event("input", { bubbles: true }));
}
