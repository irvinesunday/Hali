import { screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { jsonResponse, mockFetch, restoreFetch } from "./test/mockFetch";
import { renderWithProviders } from "./test/renderWithProviders";

// Shell-level smoke tests — every real screen fetches its own data
// from the institution backend, so these tests stub those routes
// with empty-but-valid payloads. That keeps the focus on the shell's
// structural concerns (topbar title, nav links, 404) while letting
// the screens mount without hanging on network errors.

function stubInstitutionRoutes(): void {
  mockFetch({
    "/v1/institution/overview": () =>
      jsonResponse({
        summary: { activeSignals: 0, growingSignals: 0, updatesPostedToday: 0, stabilisedToday: 0 },
        areas: [],
      }),
    "/v1/institution/clusters": () => jsonResponse({ items: [], nextCursor: null }),
    "/v1/institution/areas": () => jsonResponse({ items: [] }),
  });
}

describe("institution dashboard shell", () => {
  afterEach(() => {
    restoreFetch();
  });

  it("renders Overview as the initial screen with the matching topbar title", () => {
    stubInstitutionRoutes();
    renderWithProviders({ pathname: "/" });
    expect(screen.getByRole("heading", { level: 1, name: /overview/i })).toBeInTheDocument();
    const primaryNav = screen.getByRole("navigation", { name: /primary/i });
    expect(within(primaryNav).getByRole("link", { name: /overview/i })).toHaveAttribute(
      "aria-current",
      "page",
    );
  });

  it("exposes every primary navigation target as a link", () => {
    stubInstitutionRoutes();
    renderWithProviders({ pathname: "/" });
    const primaryNav = screen.getByRole("navigation", { name: /primary/i });
    for (const label of ["Overview", "Live Signals", "Areas", "Metrics"]) {
      expect(within(primaryNav).getByRole("link", { name: label })).toBeInTheDocument();
    }
  });

  it("titles the topbar from the active route's handle", () => {
    stubInstitutionRoutes();
    renderWithProviders({ pathname: "/signals" });
    expect(screen.getByRole("heading", { level: 1, name: /live signals/i })).toBeInTheDocument();
  });

  it("shows a distinct topbar title on unknown routes", () => {
    stubInstitutionRoutes();
    renderWithProviders({ pathname: "/this-route-does-not-exist" });
    expect(screen.getByRole("heading", { level: 1, name: /page not found/i })).toBeInTheDocument();
    expect(screen.getByRole("alert")).toHaveTextContent(/page not found/i);
  });
});
