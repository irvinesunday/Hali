import { render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { Sidebar } from "./Sidebar";
import { primaryNavigation } from "./navigation";

function renderAt(pathname: string) {
  return render(
    <MemoryRouter initialEntries={[pathname]}>
      <Sidebar />
    </MemoryRouter>,
  );
}

describe("Sidebar", () => {
  it("renders every primary navigation target", () => {
    renderAt("/");
    const nav = screen.getByRole("navigation", { name: /primary/i });
    for (const item of primaryNavigation) {
      expect(within(nav).getByRole("link", { name: item.label })).toBeInTheDocument();
    }
  });

  it("marks the link matching the current path as the current page", () => {
    renderAt("/signals");
    const link = screen.getByRole("link", { name: /live signals/i });
    expect(link).toHaveAttribute("aria-current", "page");
  });

  it("marks Overview as current only at exactly /", () => {
    renderAt("/signals");
    const overview = screen.getByRole("link", { name: /overview/i });
    expect(overview).not.toHaveAttribute("aria-current", "page");
  });
});
