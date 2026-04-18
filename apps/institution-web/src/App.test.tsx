import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import App from "./App";

describe("App", () => {
  it("renders the institution shell with Overview as the initial screen", () => {
    render(<App />);

    expect(screen.getByRole("heading", { level: 1, name: /overview/i })).toBeInTheDocument();

    const primaryNav = screen.getByRole("navigation", { name: /primary/i });
    expect(within(primaryNav).getByRole("link", { name: /overview/i })).toHaveAttribute(
      "aria-current",
      "page",
    );
  });

  it("exposes every primary navigation target as a link", () => {
    render(<App />);

    const primaryNav = screen.getByRole("navigation", { name: /primary/i });
    const labels = ["Overview", "Live Signals", "Areas", "Metrics"];
    for (const label of labels) {
      expect(within(primaryNav).getByRole("link", { name: label })).toBeInTheDocument();
    }
  });
});
