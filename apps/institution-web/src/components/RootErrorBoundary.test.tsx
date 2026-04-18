import { render, screen } from "@testing-library/react";
import { afterAll, beforeAll, describe, expect, it, vi } from "vitest";
import { RootErrorBoundary } from "./RootErrorBoundary";

function Exploder(): never {
  throw new Error("kaboom");
}

describe("RootErrorBoundary", () => {
  // React logs the caught error to the console; swallow it so the test
  // output stays clean. Keep the spy handle and restore via
  // mockRestore() — reassigning console.error directly leaves the spy
  // bookkeeping behind and can leak between test files.
  let errorSpy: ReturnType<typeof vi.spyOn>;
  beforeAll(() => {
    errorSpy = vi.spyOn(console, "error").mockImplementation(() => {});
  });
  afterAll(() => {
    errorSpy.mockRestore();
  });

  it("renders children when nothing throws", () => {
    render(
      <RootErrorBoundary>
        <p>stable</p>
      </RootErrorBoundary>,
    );
    expect(screen.getByText("stable")).toBeInTheDocument();
  });

  it("renders the recovery surface when a child throws", () => {
    render(
      <RootErrorBoundary>
        <Exploder />
      </RootErrorBoundary>,
    );
    expect(screen.getByRole("alert")).toHaveTextContent(/something went wrong/i);
  });
});
