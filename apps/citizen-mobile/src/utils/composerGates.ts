// apps/citizen-mobile/src/utils/composerGates.ts
//
// Pure classification helpers for the signal composer's confidence gates.
// Extracted from app/(app)/compose/confirm.tsx so the logic can be unit-
// tested without loading React Native.
//
// Thresholds (see src/config/constants.ts):
//   LOCATION_CONFIDENCE_WARN_THRESHOLD  = 0.5
//   LOCATION_CONFIDENCE_AMBER_THRESHOLD = 0.8
//   CONDITION_CONFIDENCE_WARN_THRESHOLD  = 0.5 (reserved for future use)
//   CONDITION_CONFIDENCE_AMBER_THRESHOLD = 0.75 (reserved for future use)

import {
  LOCATION_CONFIDENCE_WARN_THRESHOLD,
  LOCATION_CONFIDENCE_AMBER_THRESHOLD,
  CONDITION_CONFIDENCE_WARN_THRESHOLD,
  CONDITION_CONFIDENCE_AMBER_THRESHOLD,
} from '../config/constants';

/**
 * Four-tier location confidence gate.
 *
 * For location, 'fallback' replaces the old 'required' tier whenever the
 * backend flags the preview as needing correction OR when a client-side
 * derivation of the same rule fires (low numeric confidence OR blank label).
 * Rendering 'fallback' should route the user into the place-search /
 * current-location picker UX, not just display a plain text-input warning.
 *
 *   'fallback'  — correction required; render LocationFallbackPicker
 *   'confirm'   — amber: user confirms or edits the pre-filled label
 *   'accept'    — high-confidence; no action required
 *
 * 'required' is preserved in the type union for non-location gates (the
 * condition gate below still uses it) and for backwards compatibility with
 * tests that predate C11. classifyLocationGate never returns 'required' —
 * what was previously 'required' is now 'fallback'.
 */
export type ConfidenceGate = 'required' | 'confirm' | 'accept' | 'fallback';

export interface LocationGateInput {
  confidence: number;
  /** Server-authoritative flag from SignalPreviewResponse. When present it wins. */
  requiresFallback?: boolean;
  /** The NLP-extracted label (or current edited value). Blank/whitespace-only labels are treated as low-trust even at high numeric confidence. */
  label?: string | null;
}

/**
 * Classify a location-confidence score into a UI gate tier.
 *
 * The legacy single-number signature is preserved for existing tests:
 *   classifyLocationGate(0.9)
 *
 * The C11 signature threads through the server flag and the current label:
 *   classifyLocationGate({ confidence, requiresFallback, label })
 *
 * Rule (mirrors server-side LocationFallbackPolicy):
 *   - If the server flag is true → 'fallback'
 *   - Else if confidence < WARN → 'fallback'
 *   - Else if label is blank/whitespace → 'fallback'
 *   - Else if confidence < AMBER → 'confirm'
 *   - Else → 'accept'
 */
export function classifyLocationGate(
  input: number | LocationGateInput,
): ConfidenceGate {
  const normalized: LocationGateInput =
    typeof input === 'number' ? { confidence: input } : input;

  // Server wins when it has spoken.
  if (normalized.requiresFallback === true) return 'fallback';

  if (normalized.confidence < LOCATION_CONFIDENCE_WARN_THRESHOLD) {
    return 'fallback';
  }

  // Only enforce the label check when the caller supplied a label argument
  // at all — legacy callers that pass a bare number keep their existing
  // behaviour (confidence-only tier selection).
  if (
    normalized.label !== undefined &&
    (normalized.label === null || normalized.label.trim().length === 0)
  ) {
    return 'fallback';
  }

  if (normalized.confidence < LOCATION_CONFIDENCE_AMBER_THRESHOLD) {
    return 'confirm';
  }

  return 'accept';
}

/**
 * NOT YET ENFORCED IN THE UI (see confirm.tsx docs):
 * The mobile app has no taxonomy data for a condition slug dropdown,
 * so the condition gate is currently informational only. When
 * /packages/taxonomy is integrated, Step 2 should read this
 * classification and render the same three-tier UI as location.
 */
export function classifyConditionGate(confidence: number): ConfidenceGate {
  if (confidence < CONDITION_CONFIDENCE_WARN_THRESHOLD) return 'required';
  if (confidence < CONDITION_CONFIDENCE_AMBER_THRESHOLD) return 'confirm';
  return 'accept';
}
