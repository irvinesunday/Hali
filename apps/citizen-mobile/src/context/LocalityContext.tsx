// apps/citizen-mobile/src/context/LocalityContext.tsx
//
// Ward (locality) state for the citizen app.
//
// Background: GET /v1/home does NOT accept a localityId query parameter —
// the backend derives the locality scope from the authenticated user's
// follows and merges data across all of them. The `activeLocalityId` here
// is purely a client-side UX concept for:
//   - the ward picker pill in the home header
//   - composer preview location context (later sub-session)
// It never filters the home query.
//
// The `followedLocalityIds` mirror is populated once after GET
// /v1/localities/followed succeeds, and kept in sync when the user edits
// their follows in Settings.

import React, {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
} from 'react';

export interface LocalityContextValue {
  /** Currently selected ward for UX context. Not sent to the API. */
  activeLocalityId: string | null;
  /** Mirror of GET /v1/localities/followed (max 5). */
  followedLocalityIds: string[];
  /** Has the initial follows load completed? */
  followsLoaded: boolean;
  setActiveLocalityId: (id: string | null) => void;
  setFollowedLocalityIds: (ids: string[]) => void;
}

const LocalityContext = createContext<LocalityContextValue | null>(null);

export function LocalityProvider({
  children,
}: {
  children: React.ReactNode;
}): React.ReactElement {
  const [followedLocalityIds, setFollowedLocalityIdsRaw] = useState<string[]>(
    [],
  );
  const [activeLocalityId, setActiveLocalityIdRaw] = useState<string | null>(
    null,
  );
  const [followsLoaded, setFollowsLoaded] = useState<boolean>(false);

  const setActiveLocalityId = useCallback((id: string | null) => {
    setActiveLocalityIdRaw(id);
  }, []);

  const setFollowedLocalityIds = useCallback((ids: string[]) => {
    setFollowedLocalityIdsRaw(ids);
    setFollowsLoaded(true);
    // Default the active ward to the first followed ward IF the user
    // doesn't already have one, OR if their current active ward was just
    // removed from the follow set.
    setActiveLocalityIdRaw((prev) => {
      if (prev !== null && ids.includes(prev)) return prev;
      return ids[0] ?? null;
    });
  }, []);

  const value = useMemo<LocalityContextValue>(
    () => ({
      activeLocalityId,
      followedLocalityIds,
      followsLoaded,
      setActiveLocalityId,
      setFollowedLocalityIds,
    }),
    [
      activeLocalityId,
      followedLocalityIds,
      followsLoaded,
      setActiveLocalityId,
      setFollowedLocalityIds,
    ],
  );

  return (
    <LocalityContext.Provider value={value}>
      {children}
    </LocalityContext.Provider>
  );
}

export function useLocalityContext(): LocalityContextValue {
  const ctx = useContext(LocalityContext);
  if (ctx === null) {
    throw new Error(
      'useLocalityContext must be used within a LocalityProvider',
    );
  }
  return ctx;
}
