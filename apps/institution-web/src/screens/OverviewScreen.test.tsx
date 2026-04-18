import { screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import type { InstitutionOverviewResponse } from "../api/types";
import {
  errorResponse,
  jsonResponse,
  mockFetch,
  restoreFetch,
  type RouteTable,
} from "../test/mockFetch";
import { renderWithProviders } from "../test/renderWithProviders";

function overviewRoute(routes: RouteTable): void {
  mockFetch(routes);
}

const sampleOverview: InstitutionOverviewResponse = {
  summary: {
    activeSignals: 7,
    growingSignals: 2,
    updatesPostedToday: 3,
    stabilisedToday: 1,
  },
  areas: [
    {
      id: "11111111-1111-1111-1111-111111111111",
      name: "Kilimani",
      condition: "degraded",
      activeSignals: 3,
      topCategory: "water",
      lastUpdatedAt: "2026-04-18T05:00:00Z",
    },
    {
      id: "22222222-2222-2222-2222-222222222222",
      name: "Kasarani",
      condition: "calm",
      activeSignals: 0,
      topCategory: null,
      lastUpdatedAt: null,
    },
  ],
};

describe("OverviewScreen", () => {
  afterEach(() => {
    restoreFetch();
  });

  it("renders the four summary cards with their counts", async () => {
    overviewRoute({
      "/v1/institution/overview": () => jsonResponse(sampleOverview),
    });

    renderWithProviders({ pathname: "/" });

    await waitFor(() => {
      expect(screen.getByTestId("summary-active-signals")).toHaveTextContent("7");
    });
    expect(screen.getByTestId("summary-growing-signals")).toHaveTextContent("2");
    expect(screen.getByTestId("summary-updates-today")).toHaveTextContent("3");
    expect(screen.getByTestId("summary-stabilised-today")).toHaveTextContent("1");
  });

  it("lists each area with its name and condition", async () => {
    overviewRoute({
      "/v1/institution/overview": () => jsonResponse(sampleOverview),
    });

    renderWithProviders({ pathname: "/" });

    const areas = await screen.findAllByTestId("overview-area-item");
    expect(areas).toHaveLength(2);
    expect(within(areas[0]!).getByText("Kilimani")).toBeInTheDocument();
    expect(within(areas[0]!).getByText(/degraded/i)).toBeInTheDocument();
    expect(within(areas[1]!).getByText("Kasarani")).toBeInTheDocument();
  });

  it("renders an empty state when the institution has no areas", async () => {
    overviewRoute({
      "/v1/institution/overview": () =>
        jsonResponse({
          summary: sampleOverview.summary,
          areas: [],
        }),
    });

    renderWithProviders({ pathname: "/" });

    expect(await screen.findByText(/no areas in scope yet/i)).toBeInTheDocument();
  });

  it("renders the error state and offers retry when the service fails", async () => {
    overviewRoute({
      "/v1/institution/overview": () => errorResponse(500, "boom"),
    });

    renderWithProviders({ pathname: "/" });

    await waitFor(() => {
      expect(screen.getByRole("alert")).toHaveTextContent(/couldn't load the overview/i);
    });
    expect(screen.getByRole("button", { name: /retry/i })).toBeInTheDocument();
  });
});
