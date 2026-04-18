import type { ReactNode } from "react";
import {
  FeatureFlagsContext,
  type FeatureFlagsContextValue,
} from "../featureFlags/FeatureFlagsProvider";

// Test-only provider that short-circuits the `/v1/feature-flags`
// fetch. Shell and screen tests care about the rendered surface, not
// the flag-resolution wire; mounting the real provider would force
// every test to stub the endpoint before arranging its own fixture.
//
// Default is "every flag enabled" so tests written before #205 keep
// passing without change; a test that needs the opposite can pass an
// `override` with the specific keys it wants to flip off. The
// dedicated DashboardGate / FeatureFlagsProvider tests still mount
// the real provider against a mocked fetch so the wire behaviour is
// covered.

export interface FeatureFlagsTestProviderProps {
  readonly children: ReactNode;
  readonly override?: Readonly<Record<string, boolean>>;
  readonly isLoading?: boolean;
  readonly isError?: boolean;
}

export function FeatureFlagsTestProvider({
  children,
  override,
  isLoading = false,
  isError = false,
}: FeatureFlagsTestProviderProps) {
  const value: FeatureFlagsContextValue = {
    isLoading,
    isError,
    isFlagEnabled: (key) => (override && key in override ? override[key] === true : true),
  };
  return <FeatureFlagsContext.Provider value={value}>{children}</FeatureFlagsContext.Provider>;
}
