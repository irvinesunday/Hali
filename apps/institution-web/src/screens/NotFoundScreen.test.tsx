import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { NotFoundScreen } from "./NotFoundScreen";

describe("NotFoundScreen", () => {
  it("renders a recovery link back to Overview", () => {
    render(
      <MemoryRouter>
        <NotFoundScreen />
      </MemoryRouter>,
    );
    expect(screen.getByRole("alert")).toHaveTextContent(/page not found/i);
    expect(screen.getByRole("link", { name: /return to overview/i })).toHaveAttribute("href", "/");
  });
});
