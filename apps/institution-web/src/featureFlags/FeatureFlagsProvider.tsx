import { useQuery } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { createContext, useContext, useMemo } from "react";
import {
  getResolvedFeatureFlags,
  InstitutionWebFlagKeys,
  type InstitutionWebFlagKey,
} from "../api/featureFlags";

// Feature-flag context for the institution dashboard.
//
// The provider wraps the router and issues a single `/v1/feature-flags`
// request at mount, caching the result in TanStack Query. A loading
// fallback blanks the shell while flags resolve — a flash of enabled-
// UI would undermine the master gate (users could click into a
// disabled feature during the window before the flags arrive).
//
// On error we fall back to "all off" rather than "all on": the master
// gate (`institution_web.enabled`) being off surfaces the locked
// screen, which is the correct conservative behaviour when the flag
// service is unreachable. This matches the "safe disablement" line
// in #205's acceptance criteria.
//
// Flag lookups always go through the typed `useFeatureFlag(key)` hook
// so consumers can't accidentally depend on a bare string key; adding
// a new flag is one spot in `api/featureFlags.ts` plus the backend
// catalog.

export interface FeatureFlagsContextValue {
  readonly isLoading: boolean;
  readonly isError: boolean;
  readonly isFlagEnabled: (key: InstitutionWebFlagKey) => boolean;
}

const defaultContext: FeatureFlagsContextValue = {
  isLoading: true,
  isError: false,
  isFlagEnabled: () => false,
};

export const FeatureFlagsContext = createContext<FeatureFlagsContextValue>(defaultContext);

export interface FeatureFlagsProviderProps {
  readonly children: ReactNode;
}

export function FeatureFlagsProvider({ children }: FeatureFlagsProviderProps) {
  const query = useQuery({
    queryKey: ["feature-flags", "institution"] as const,
    queryFn: getResolvedFeatureFlags,
    staleTime: 60_000,
    refetchOnWindowFocus: false,
    retry: 1,
  });

  const value = useMemo<FeatureFlagsContextValue>(() => {
    const flagMap = query.data?.flags ?? {};
    return {
      isLoading: query.isLoading,
      isError: query.isError,
      isFlagEnabled: (key) => flagMap[key] === true,
    };
  }, [query.data, query.isLoading, query.isError]);

  return (
    <FeatureFlagsContext.Provider value={value}>{children}</FeatureFlagsContext.Provider>
  );
}

export function useFeatureFlag(key: InstitutionWebFlagKey): boolean {
  const { isFlagEnabled } = useContext(FeatureFlagsContext);
  return isFlagEnabled(key);
}

export function useFeatureFlagsStatus(): Pick<
  FeatureFlagsContextValue,
  "isLoading" | "isError"
> {
  const { isLoading, isError } = useContext(FeatureFlagsContext);
  return { isLoading, isError };
}

// Re-export the flag-key constants so call sites only import from one
// place.
export { InstitutionWebFlagKeys };
