// apps/citizen-mobile/src/context/LocalityContext.tsx
//
// Ward (locality) state for the citizen app.
//
// The active locality is sent to GET /v1/home?localityId=... so the backend
// scopes the feed to that single locality. When null, the backend falls back
// to the authenticated user's full followed-localities set.
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
  /** Currently selected ward — sent to GET /v1/home as localityId. */
  activeLocality: FollowedLocality | null;
  /** Mirror of GET /v1/localities/followed (max 5). */
  followedLocalities: FollowedLocality[];
  /** Has the initial follows load completed? */
  followsLoaded: boolean;
  setActiveLocalityId: (id: string | null) => void;
  setFollowedLocalities: (items: FollowedLocality[]) => void;
  /**
   * Set the active locality directly — used by anonymous browse to select a
   * locality from search results without requiring it to be in the followed set.
   */
  setActiveLocality: (locality: FollowedLocality | null) => void;
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

  const setActiveLocality = useCallback(
    (locality: FollowedLocality | null) => {
      setActiveLocalityRaw(locality);
    },
    [],
  );

  const value = useMemo<LocalityContextValue>(
    () => ({
      activeLocality,
      followedLocalities,
      followsLoaded,
      setActiveLocalityId,
      setFollowedLocalities,
      setActiveLocality,
    }),
    [
      activeLocality,
      followedLocalities,
      followsLoaded,
      setActiveLocalityId,
      setFollowedLocalities,
      setActiveLocality,
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
