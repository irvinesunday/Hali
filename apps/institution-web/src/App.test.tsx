import { render, screen, within } from "@testing-library/react";
import { RouterProvider, createMemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { institutionRoutes } from "./router";

function renderAt(pathname: string) {
  const router = createMemoryRouter(institutionRoutes, { initialEntries: [pathname] });
  return render(<RouterProvider router={router} />);
}

describe("institution dashboard shell", () => {
  it("renders Overview as the initial screen with the matching topbar title", () => {
    renderAt("/");
    expect(screen.getByRole("heading", { level: 1, name: /overview/i })).toBeInTheDocument();
    const primaryNav = screen.getByRole("navigation", { name: /primary/i });
    expect(within(primaryNav).getByRole("link", { name: /overview/i })).toHaveAttribute(
      "aria-current",
      "page",
    );
  });

  it("exposes every primary navigation target as a link", () => {
    renderAt("/");
    const primaryNav = screen.getByRole("navigation", { name: /primary/i });
    for (const label of ["Overview", "Live Signals", "Areas", "Metrics"]) {
      expect(within(primaryNav).getByRole("link", { name: label })).toBeInTheDocument();
    }
  });

  it("titles the topbar from the active route's handle", () => {
    renderAt("/signals");
    expect(screen.getByRole("heading", { level: 1, name: /live signals/i })).toBeInTheDocument();
  });

  it("shows a distinct topbar title on unknown routes", () => {
    renderAt("/this-route-does-not-exist");
    expect(screen.getByRole("heading", { level: 1, name: /page not found/i })).toBeInTheDocument();
    expect(screen.getByRole("alert")).toHaveTextContent(/page not found/i);
  });
});
