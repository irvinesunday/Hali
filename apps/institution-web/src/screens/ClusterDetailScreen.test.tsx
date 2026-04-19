import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it } from "vitest";
import type { ClusterDetailResponse } from "../api/types";
import { InstitutionWebFlagKeys } from "../featureFlags/FeatureFlagsProvider";
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
  restorationRatio: null,
  restorationYesVotes: null,
  restorationTotalVotes: null,
  officialPosts: [
    {
      id: "post-1",
      institutionId: "inst-1",
      type: "live_update",
      category: "electricity",
      title: "Teams dispatched to Ngong Road substation",
      body: "Crews are en route to the substation. ETA 30 minutes.",
      startsAt: null,
      endsAt: null,
      status: "published",
      relatedClusterId: clusterId,
      isRestorationClaim: false,
      createdAt: "2026-04-18T04:00:00Z",
      responseStatus: "teams_dispatched",
      severity: null,
    },
  ],
};

describe("ClusterDetailScreen", () => {
  afterEach(() => {
    restoreFetch();
  });

  it("renders the cluster title, location, and key detail fields", async () => {
    mockFetch({
      [`/v1/institution/clusters/${clusterId}`]: () => jsonResponse(sampleCluster),
    });

    renderWithProviders({ pathname: `/signals/${clusterId}` });

    expect(
      await screen.findByRole("heading", { level: 2, name: /power outage on ngong road/i }),
    ).toBeInTheDocument();
    expect(screen.getByText(/ngong road near adams arcade/i)).toBeInTheDocument();
    expect(screen.getAllByText(/teams_dispatched/).length).toBeGreaterThan(0);
    expect(screen.getByText("18")).toBeInTheDocument();
    expect(screen.getByText("6")).toBeInTheDocument();
  });

  it("renders existing official updates for this cluster", async () => {
    mockFetch({
      [`/v1/institution/clusters/${clusterId}`]: () => jsonResponse(sampleCluster),
    });

    renderWithProviders({ pathname: `/signals/${clusterId}` });

    const card = await screen.findByTestId("official-post-card");
    expect(card).toHaveTextContent(/teams dispatched to ngong road substation/i);
    expect(card).toHaveTextContent(/live update/i);
  });

  it("shows an empty-state hint when the cluster has no official posts yet", async () => {
    mockFetch({
      [`/v1/institution/clusters/${clusterId}`]: () =>
        jsonResponse({ ...sampleCluster, officialPosts: [] }),
    });

    renderWithProviders({ pathname: `/signals/${clusterId}` });

    expect(
      await screen.findByText(/no official updates posted yet/i),
    ).toBeInTheDocument();
  });

  it("opens the post-update modal when the Post an update button is clicked", async () => {
    mockFetch({
      [`/v1/institution/clusters/${clusterId}`]: () => jsonResponse(sampleCluster),
    });

    renderWithProviders({ pathname: `/signals/${clusterId}` });

    const trigger = await screen.findByRole("button", { name: /post an update/i });
    await userEvent.click(trigger);

    expect(screen.getByRole("dialog")).toHaveTextContent(/post an official update/i);
  });

  it("renders the error state when the cluster is out of scope or missing", async () => {
    mockFetch({
      [`/v1/institution/clusters/${clusterId}`]: () => errorResponse(404),
    });

    renderWithProviders({ pathname: `/signals/${clusterId}` });

    await waitFor(() => {
      expect(screen.getByRole("alert")).toHaveTextContent(/couldn't load this signal/i);
    });
  });

  it("hides the Post an update trigger when the post-update kill switch is off", async () => {
    mockFetch({
      [`/v1/institution/clusters/${clusterId}`]: () => jsonResponse(sampleCluster),
    });

    renderWithProviders({
      pathname: `/signals/${clusterId}`,
      flagOverride: { [InstitutionWebFlagKeys.postUpdateEnabled]: false },
    });

    await screen.findByRole("heading", { level: 2, name: /power outage on ngong road/i });
    expect(screen.queryByRole("button", { name: /post an update/i })).not.toBeInTheDocument();
  });

  it("hides the Mark as restored trigger when the restoration kill switch is off", async () => {
    mockFetch({
      [`/v1/institution/clusters/${clusterId}`]: () => jsonResponse(sampleCluster),
    });

    renderWithProviders({
      pathname: `/signals/${clusterId}`,
      flagOverride: { [InstitutionWebFlagKeys.restorationClaimEnabled]: false },
    });

    await screen.findByRole("heading", { level: 2, name: /power outage on ngong road/i });
    expect(screen.queryByRole("button", { name: /mark as restored/i })).not.toBeInTheDocument();
  });
});
