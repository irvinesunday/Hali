import { screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import type { ClusterDetailResponse } from "../api/types";
import { errorResponse, jsonResponse, mockFetch, restoreFetch } from "../test/mockFetch";
import { renderWithProviders } from "../test/renderWithProviders";

const clusterId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";

const sampleCluster: ClusterDetailResponse = {
  id: clusterId,
  state: "active",
  category: "electricity",
  subcategorySlug: "outage",
  title: "Power outage on Ngong Road",
  summary: "Several blocks along Ngong Road report no power since early afternoon.",
  affectedCount: 18,
  observingCount: 6,
  createdAt: "2026-04-18T03:00:00Z",
  updatedAt: "2026-04-18T05:00:00Z",
  activatedAt: "2026-04-18T03:30:00Z",
  possibleRestorationAt: null,
  resolvedAt: null,
  locationLabel: "Ngong Road near Adams Arcade, Kilimani",
  responseStatus: "teams_dispatched",
};

describe("ClusterDetailScreen", () => {
  afterEach(() => {
    restoreFetch();
  });

  it("renders the cluster title, location, and key detail fields", async () => {
    mockFetch({
      [`/v1/institution/signals/${clusterId}`]: () => jsonResponse(sampleCluster),
    });

    renderWithProviders({ pathname: `/signals/${clusterId}` });

    expect(
      await screen.findByRole("heading", { level: 2, name: /power outage on ngong road/i }),
    ).toBeInTheDocument();
    expect(screen.getByText(/ngong road near adams arcade/i)).toBeInTheDocument();
    expect(screen.getByText(/teams_dispatched/)).toBeInTheDocument();
    expect(screen.getByText("18")).toBeInTheDocument();
    expect(screen.getByText("6")).toBeInTheDocument();
  });

  it("renders the error state when the cluster is out of scope or missing", async () => {
    mockFetch({
      [`/v1/institution/signals/${clusterId}`]: () => errorResponse(404),
    });

    renderWithProviders({ pathname: `/signals/${clusterId}` });

    await waitFor(() => {
      expect(screen.getByRole("alert")).toHaveTextContent(/couldn't load this signal/i);
    });
  });
});
