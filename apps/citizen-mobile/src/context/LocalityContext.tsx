import React, { createContext, useContext, useState, useCallback } from 'react';

interface LocalityContextValue {
  activeLocalityId: string | null;
  followedLocalityIds: string[];
  setActiveLocalityId: (id: string) => void;
  setFollowedLocalityIds: (ids: string[]) => void;
}

const LocalityContext = createContext<LocalityContextValue | null>(null);

export function LocalityProvider({ children }: { children: React.ReactNode }) {
  const [followedLocalityIds, setFollowedLocalityIds] = useState<string[]>([]);
  const [activeLocalityId, setActiveLocalityIdRaw] = useState<string | null>(
    null,
  );

  const setActiveLocalityId = useCallback((id: string) => {
    setActiveLocalityIdRaw(id);
  }, []);

  const handleSetFollowed = useCallback((ids: string[]) => {
    setFollowedLocalityIds(ids);
    // First ward followed becomes the active home ward
    setActiveLocalityIdRaw((prev) => (prev ? prev : ids[0] ?? null));
  }, []);

  return (
    <LocalityContext.Provider
      value={{
        activeLocalityId,
        followedLocalityIds,
        setActiveLocalityId,
        setFollowedLocalityIds: handleSetFollowed,
      }}
    >
      {children}
    </LocalityContext.Provider>
  );
}

export function useLocalityContext(): LocalityContextValue {
  const ctx = useContext(LocalityContext);
  if (!ctx)
    throw new Error('useLocalityContext must be used within LocalityProvider');
  return ctx;
}
