import React, { createContext, useContext, useState, useCallback } from 'react';
import type { SignalLocationSource, SignalPreviewResponse } from '../types/api';

/**
 * Authoritative coordinates + label produced by the C11 low-confidence
 * location fallback picker. When present on the composer draft, the
 * submit screen uses these values instead of the device-GPS coordinates
 * and the NLP-derived label.
 *
 * Set when:
 *   - the user picks a PlaceCandidate from /v1/places/search → source = 'place_search'
 *   - the user taps "Use my current location" → source = 'place_search'
 *     (the label comes from /v1/places/reverse, i.e. the same backend
 *     geocoding service; the coordinates are device GPS but treated as
 *     authoritative because the user deliberately chose them)
 *
 * NOT set when the user merely edits the text label inline — that path
 * stays on source='user_edit' with device-GPS coordinates on submit.
 */
export interface ComposerLocationOverride {
  latitude: number;
  longitude: number;
  label: string;
  source: SignalLocationSource;
}

interface ComposerContextValue {
  freeText: string;
  locationHint: string | undefined;
  preview: SignalPreviewResponse | null;
  deviceHash: string;
  locationOverride: ComposerLocationOverride | null;
  setFreeText: (v: string) => void;
  setLocationHint: (v: string | undefined) => void;
  setPreview: (p: SignalPreviewResponse | null) => void;
  setDeviceHash: (h: string) => void;
  setLocationOverride: (o: ComposerLocationOverride | null) => void;
  reset: () => void;
}

const ComposerContext = createContext<ComposerContextValue | null>(null);

export function ComposerProvider({ children }: { children: React.ReactNode }) {
  const [freeText, setFreeText] = useState('');
  const [locationHint, setLocationHint] = useState<string | undefined>();
  const [preview, setPreview] = useState<SignalPreviewResponse | null>(null);
  const [deviceHash, setDeviceHash] = useState('');
  const [locationOverride, setLocationOverride] =
    useState<ComposerLocationOverride | null>(null);

  const reset = useCallback(() => {
    setFreeText('');
    setLocationHint(undefined);
    setPreview(null);
    setLocationOverride(null);
  }, []);

  return (
    <ComposerContext.Provider
      value={{
        freeText,
        locationHint,
        preview,
        deviceHash,
        locationOverride,
        setFreeText,
        setLocationHint,
        setPreview,
        setDeviceHash,
        setLocationOverride,
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
