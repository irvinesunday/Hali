// apps/citizen-mobile/src/context/LocalityContext.tsx
//
// Ward (locality) state for the citizen app.
//
// Background: GET /v1/home does NOT accept a localityId query parameter —
// the backend derives the locality scope from the authenticated user's
// follows and merges data across all of them. The active locality here
// is purely a client-side UX concept for:
//   - the ward picker pill in the home header
//   - composer preview location context (later sub-session)
// It never filters the home query.
//
// The followedLocalities mirror is populated once after GET
// /v1/localities/followed succeeds, and kept in sync when the user edits
// their follows in Settings.

import React, {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
} from 'react';
import type { FollowedLocality } from '../types/api';

export interface LocalityContextValue {
  /** Currently selected ward for UX context. Not sent to the API. */
  activeLocality: FollowedLocality | null;
  /** Mirror of GET /v1/localities/followed (max 5). */
  followedLocalities: FollowedLocality[];
  /** Has the initial follows load completed? */
  followsLoaded: boolean;
  setActiveLocalityId: (id: string | null) => void;
  setFollowedLocalities: (items: FollowedLocality[]) => void;
}

const LocalityContext = createContext<LocalityContextValue | null>(null);

export function LocalityProvider({
  children,
}: {
  children: React.ReactNode;
}): React.ReactElement {
  const [followedLocalities, setFollowedLocalitiesRaw] = useState<
    FollowedLocality[]
  >([]);
  const [activeLocality, setActiveLocalityRaw] =
    useState<FollowedLocality | null>(null);
  const [followsLoaded, setFollowsLoaded] = useState<boolean>(false);

  const setActiveLocalityId = useCallback(
    (id: string | null) => {
      setActiveLocalityRaw((prev) => {
        if (id === null) return null;
        const match = followedLocalities.find((l) => l.localityId === id);
        return match ?? prev;
      });
    },
    [followedLocalities],
  );

  const setFollowedLocalities = useCallback((items: FollowedLocality[]) => {
    setFollowedLocalitiesRaw(items);
    setFollowsLoaded(true);
    // Default the active locality to the first followed one IF the user
    // doesn't already have one, OR if their current active locality was
    // just removed from the follow set.
    setActiveLocalityRaw((prev) => {
      if (prev !== null) {
        const stillThere = items.find((l) => l.localityId === prev.localityId);
        if (stillThere) return stillThere;
      }
      return items[0] ?? null;
    });
  }, []);

  const value = useMemo<LocalityContextValue>(
    () => ({
      activeLocality,
      followedLocalities,
      followsLoaded,
      setActiveLocalityId,
      setFollowedLocalities,
    }),
    [
      activeLocality,
      followedLocalities,
      followsLoaded,
      setActiveLocalityId,
      setFollowedLocalities,
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
