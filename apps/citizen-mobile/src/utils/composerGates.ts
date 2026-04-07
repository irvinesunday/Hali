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
 * Three-tier confidence gate:
 *   'required' — confidence below WARN: user MUST take action
 *   'confirm'  — confidence between WARN and AMBER: user confirms or edits
 *   'accept'   — confidence at or above AMBER: no action required
 */
export type ConfidenceGate = 'required' | 'confirm' | 'accept';

export function classifyLocationGate(confidence: number): ConfidenceGate {
  if (confidence < LOCATION_CONFIDENCE_WARN_THRESHOLD) return 'required';
  if (confidence < LOCATION_CONFIDENCE_AMBER_THRESHOLD) return 'confirm';
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
