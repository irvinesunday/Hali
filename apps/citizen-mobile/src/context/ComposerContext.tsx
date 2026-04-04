import React, { createContext, useContext, useState, useCallback } from 'react';
import type { SignalPreviewResponse } from '../types/api';

interface ComposerContextValue {
  freeText: string;
  locationHint: string | undefined;
  preview: SignalPreviewResponse | null;
  deviceHash: string;
  setFreeText: (v: string) => void;
  setLocationHint: (v: string | undefined) => void;
  setPreview: (p: SignalPreviewResponse | null) => void;
  setDeviceHash: (h: string) => void;
  reset: () => void;
}

const ComposerContext = createContext<ComposerContextValue | null>(null);

export function ComposerProvider({ children }: { children: React.ReactNode }) {
  const [freeText, setFreeText] = useState('');
  const [locationHint, setLocationHint] = useState<string | undefined>();
  const [preview, setPreview] = useState<SignalPreviewResponse | null>(null);
  const [deviceHash, setDeviceHash] = useState('');

  const reset = useCallback(() => {
    setFreeText('');
    setLocationHint(undefined);
    setPreview(null);
  }, []);

  return (
    <ComposerContext.Provider
      value={{
        freeText,
        locationHint,
        preview,
        deviceHash,
        setFreeText,
        setLocationHint,
        setPreview,
        setDeviceHash,
        reset,
      }}
    >
      {children}
    </ComposerContext.Provider>
  );
}

export function useComposerContext(): ComposerContextValue {
  const ctx = useContext(ComposerContext);
  if (!ctx)
    throw new Error('useComposerContext must be used within ComposerProvider');
  return ctx;
}
