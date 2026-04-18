import { screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import type { InstitutionAreasResponse } from "../api/types";
import { errorResponse, jsonResponse, mockFetch, restoreFetch } from "../test/mockFetch";
import { renderWithProviders } from "../test/renderWithProviders";

const sampleAreas: InstitutionAreasResponse = {
  items: [
    {
      id: "a-1",
      name: "Kilimani",
      condition: "degraded",
      activeSignals: 3,
      topCategory: "water",
      lastUpdatedAt: null,
    },
    {
      id: "a-2",
      name: "Lang'ata",
      condition: "calm",
      activeSignals: 0,
      topCategory: null,
      lastUpdatedAt: null,
    },
  ],
};

describe("AreasScreen", () => {
  afterEach(() => {
    restoreFetch();
  });

  it("renders one card per area with its name and active signal count", async () => {
    mockFetch({
      "/v1/institution/areas": () => jsonResponse(sampleAreas),
    });

    renderWithProviders({ pathname: "/areas" });

    const items = await screen.findAllByTestId("area-item");
    expect(items).toHaveLength(2);
    expect(items[0]!).toHaveTextContent("Kilimani");
    expect(items[0]!).toHaveTextContent(/3 active signals/i);
    expect(items[1]!).toHaveTextContent("Lang'ata");
  });

  it("shows the empty state when no areas are assigned", async () => {
    mockFetch({
      "/v1/institution/areas": () => jsonResponse({ items: [] }),
    });

    renderWithProviders({ pathname: "/areas" });

    expect(await screen.findByText(/no areas assigned/i)).toBeInTheDocument();
  });

  it("renders the error state when the areas service fails", async () => {
    mockFetch({
      "/v1/institution/areas": () => errorResponse(500),
    });

    renderWithProviders({ pathname: "/areas" });

    await waitFor(() => {
      expect(screen.getByRole("alert")).toHaveTextContent(/couldn't load your areas/i);
    });
  });
});
