import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { FeatureFlagsTestProvider } from "../test/featureFlagsTestProvider";
import { DashboardGate } from "./DashboardGate";
import { InstitutionWebFlagKeys } from "./FeatureFlagsProvider";

// The gate consumes `useFeatureFlag` + `useFeatureFlagsStatus`; we
// drive each state deterministically via `FeatureFlagsTestProvider`
// so the assertions don't hinge on async fetch timing (the wire
// behaviour is covered in FeatureFlagsProvider.test.tsx).

describe("DashboardGate", () => {
  it("renders the loading surface while flags resolve", () => {
    render(
      <FeatureFlagsTestProvider isLoading>
        <DashboardGate>
          <span>shell</span>
        </DashboardGate>
      </FeatureFlagsTestProvider>,
    );
    const status = screen.getByTestId("dashboard-gate-status");
    expect(status).toHaveAttribute("data-state", "loading");
    expect(status).toHaveTextContent(/resolving dashboard access/i);
    expect(screen.queryByText("shell")).not.toBeInTheDocument();
  });

  it("renders the disabled surface when the master flag is off", () => {
    render(
      <FeatureFlagsTestProvider
        override={{ [InstitutionWebFlagKeys.enabled]: false }}
      >
        <DashboardGate>
          <span>shell</span>
        </DashboardGate>
      </FeatureFlagsTestProvider>,
    );
    const status = screen.getByTestId("dashboard-gate-status");
    expect(status).toHaveAttribute("data-state", "disabled");
    expect(status).toHaveTextContent(/currently unavailable/i);
    expect(screen.queryByText("shell")).not.toBeInTheDocument();
  });

  it("renders its children when the master flag is enabled", () => {
    render(
      <FeatureFlagsTestProvider
        override={{ [InstitutionWebFlagKeys.enabled]: true }}
      >
        <DashboardGate>
          <span>shell</span>
        </DashboardGate>
      </FeatureFlagsTestProvider>,
    );
    expect(screen.getByText("shell")).toBeInTheDocument();
    expect(screen.queryByTestId("dashboard-gate-status")).not.toBeInTheDocument();
  });
});
