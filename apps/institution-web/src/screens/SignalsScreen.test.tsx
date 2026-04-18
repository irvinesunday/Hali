import { screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import type { InstitutionSignalsResponse } from "../api/types";
import { errorResponse, jsonResponse, mockFetch, restoreFetch } from "../test/mockFetch";
import { renderWithProviders } from "../test/renderWithProviders";

const sampleSignals: InstitutionSignalsResponse = {
  items: [
    {
      id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      title: "Power outage on Ngong Road",
      area: { id: "area-1", name: "Kilimani" },
      category: "electricity",
      condition: "major",
      trend: "growing",
      responseStatus: "teams_dispatched",
      affectedCount: 12,
      recentReports24h: 14,
      timeActiveSeconds: 4_500,
    },
    {
      id: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
      title: "Water supply intermittent",
      area: null,
      category: "water",
      condition: "moderate",
      trend: "steady",
      responseStatus: null,
      affectedCount: 4,
      recentReports24h: 5,
      timeActiveSeconds: 300,
    },
  ],
  nextCursor: null,
};

describe("SignalsScreen", () => {
  afterEach(() => {
    restoreFetch();
  });

  it("renders a row per cluster with category, condition, and trend metrics", async () => {
    mockFetch({
      "/v1/institution/signals": () => jsonResponse(sampleSignals),
    });

    renderWithProviders({ pathname: "/signals" });

    const rows = await screen.findAllByTestId("signal-row");
    expect(rows).toHaveLength(2);
    expect(rows[0]!).toHaveTextContent(/power outage on ngong road/i);
    expect(rows[0]!).toHaveTextContent(/electricity/i);
    expect(rows[0]!).toHaveTextContent(/growing/i);
    expect(rows[0]!).toHaveTextContent(/teams_dispatched/i);
  });

  it("links each row to its cluster detail route", async () => {
    mockFetch({
      "/v1/institution/signals": () => jsonResponse(sampleSignals),
    });

    renderWithProviders({ pathname: "/signals" });

    const link = await screen.findByRole("link", { name: /power outage on ngong road/i });
    expect(link).toHaveAttribute("href", "/signals/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
  });

  it("shows the empty state when the institution has no active signals", async () => {
    mockFetch({
      "/v1/institution/signals": () => jsonResponse({ items: [], nextCursor: null }),
    });

    renderWithProviders({ pathname: "/signals" });

    expect(await screen.findByText(/no active signals in your scope/i)).toBeInTheDocument();
  });

  it("renders the error state when the list service fails", async () => {
    mockFetch({
      "/v1/institution/signals": () => errorResponse(503),
    });

    renderWithProviders({ pathname: "/signals" });

    await waitFor(() => {
      expect(screen.getByRole("alert")).toHaveTextContent(/couldn't load your signals/i);
    });
  });
});
