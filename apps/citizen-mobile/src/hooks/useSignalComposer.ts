import { useState, useCallback } from 'react';
import { useMutation } from '@tanstack/react-query';
import * as Crypto from 'expo-crypto';
import * as Location from 'expo-location';
import { previewSignal, submitSignal } from '../api/signals';
import type { SignalPreviewResponse, SignalSubmitRequest } from '../types/api';
import type { ComposerState } from '../types/domain';

const INITIAL_STATE: ComposerState = {
  freeText: '',
  locationHint: undefined,
  preview: null,
  idempotencyKey: null,
};

export function useSignalComposer(deviceHash: string) {
  const [state, setState] = useState<ComposerState>(INITIAL_STATE);

  const reset = useCallback(() => setState(INITIAL_STATE), []);

  const previewMutation = useMutation({
    mutationFn: async ({
      freeText,
      locationHint,
    }: {
      freeText: string;
      locationHint?: string;
    }) => {
      const result = await previewSignal({
        freeText,
        selectedWard: locationHint,
        countryCode: 'KE',
      });
      return { freeText, locationHint, result };
    },
    onSuccess: ({ freeText, locationHint, result }) => {
      setState((prev) => ({
        ...prev,
        freeText,
        locationHint,
        preview: result,
      }));
    },
  });

  const submitMutation = useMutation({
    mutationFn: async (overrides?: Partial<SignalPreviewResponse>) => {
      const preview = { ...state.preview, ...overrides } as SignalPreviewResponse;

      const { status } = await Location.requestForegroundPermissionsAsync();
      if (status !== 'granted') throw new Error('Location permission required');
      const position = await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.Balanced });

      // Generate a fresh idempotency key per submit attempt
      const idempotencyKey = await Crypto.digestStringAsync(
        Crypto.CryptoDigestAlgorithm.SHA256,
        `${state.freeText}:${Date.now()}`,
      );

      const body: SignalSubmitRequest = {
        idempotencyKey,
        deviceHash,
        freeText: state.freeText,
        category: preview.category,
        subcategorySlug: preview.subcategorySlug,
        conditionSlug: preview.conditionSlug ?? undefined,
        conditionConfidence: preview.conditionConfidence,
        latitude: position.coords.latitude,
        longitude: position.coords.longitude,
        locationLabel: preview.location.locationLabel ?? undefined,
        locationPrecisionType:
          preview.location.locationPrecisionType ?? undefined,
        locationConfidence: preview.location.locationConfidence,
        locationSource: preview.location.locationSource,
        temporalType: preview.temporalType ?? undefined,
        neutralSummary: preview.neutralSummary ?? undefined,
      };

      return submitSignal(body);
    },
    onSuccess: () => {
      setState((prev) => ({
        ...prev,
        idempotencyKey: null,
      }));
    },
  });

  return { state, setState, reset, previewMutation, submitMutation };
}
