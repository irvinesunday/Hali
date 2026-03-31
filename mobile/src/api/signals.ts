/**
 * Signals API stubs — aligned to patched OpenAPI spec.
 *
 * POST /v1/signals/preview
 * POST /v1/signals/submit   (was /v1/signals)
 */

import { apiFetch } from './client';

export type SignalLocationInput = {
  latitude?: number;
  longitude?: number;
  userEnteredPlace?: string;
};

export type SignalPreviewRequest = {
  text: string;
  sourceLanguage: string;
  location?: SignalLocationInput;
};

export type SignalCandidate = {
  category: string;
  subcategorySlug: string;
  conditionSlug: string | null;
  neutralSummary: string;
  locationLabel: string;
  locationConfidence: number;
  temporalType: string;
  conditionConfidence: number | null;
  locationSource: 'nlp' | 'search' | 'pin' | null;
  locationPrecisionType: string | null;
};

export type ExistingClusterCandidate = {
  clusterId: string;
  summary: string;
  locationLabel: string;
  rawConfirmationCount: number;
};

export type SignalPreviewResponse = {
  candidates: SignalCandidate[];
  existingClusterCandidates: ExistingClusterCandidate[];
};

export type SignalSubmitRequest = {
  candidate: SignalCandidate;
  joinClusterId: string | null;
  deviceFingerprint: string;
  idempotencyKey: string;
};

export type SignalSubmitResponse = {
  signalEventId: string;
  clusterId: string | null;
};

export function previewSignal(
  body: SignalPreviewRequest,
  idempotencyKey: string,
): Promise<SignalPreviewResponse> {
  return apiFetch('/v1/signals/preview', {
    method: 'POST',
    body: JSON.stringify(body),
    idempotencyKey,
  });
}

export function submitSignal(
  body: SignalSubmitRequest,
  idempotencyKey: string,
): Promise<SignalSubmitResponse> {
  return apiFetch('/v1/signals/submit', {
    method: 'POST',
    body: JSON.stringify(body),
    idempotencyKey,
  });
}
