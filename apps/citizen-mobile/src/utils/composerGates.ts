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

/**
 * Did the user *meaningfully* edit the composer's location label?
 *
 * Semantics — both sides are trimmed before comparison:
 *   - whitespace-only edits don't count
 *   - blanking the field doesn't count (no new user-authored content)
 *   - any non-empty text that differs from the trimmed original counts
 *
 * Used in two places:
 *   - {@link canProceedFromLocationGate}'s 'confirm' tier uses this to
 *     decide whether to unlock Next without the explicit "Looks right"
 *     chip, and
 *   - the composer's `handleNext` uses it to flip the submit wire
 *     `locationSource` from `'nlp'` to `'user_edit'` so source
 *     attribution matches the label's provenance (C11 follow-up #131).
 *
 * Keeping a single helper prevents the two paths from drifting — if
 * the gate thinks "you edited this" but the submit path thinks "you
 * didn't", we'd submit user-authored text under `source='nlp'`.
 */
export interface MeaningfulEditInput {
  label: string;
  originalLabel: string | null | undefined;
}

export function isMeaningfulLabelEdit(input: MeaningfulEditInput): boolean {
  const trimmed = input.label.trim();
  const original = (input.originalLabel ?? '').trim();
  return trimmed.length > 0 && trimmed !== original;
}

/**
 * Inputs to {@link canProceedFromLocationGate}. Mirrors the state the
 * composer's Step 2 carries internally (text-input value + the original
 * NLP label + whether the user tapped "Looks right" + whether the
 * picker produced an override).
 */
export interface LocationProceedInput {
  /** Gate currently classifying the location state. */
  gate: ConfidenceGate;
  /** True when an authoritative override has been picked via the fallback picker. */
  hasOverride: boolean;
  /** Current value of the text-input label (confirm / accept tiers only). */
  label: string;
  /** The NLP-extracted label the preview first showed; used to detect user edits. */
  originalLabel: string | null | undefined;
  /** True after the user taps "Looks right" on the amber 'confirm' tier. */
  confirmed: boolean;
}

/**
 * Whether the composer's "Next" button should be enabled given the
 * current location state.
 *
 * Pure function — extracted from confirm.tsx so the fallback/override
 * interaction can be unit-tested without loading React Native.
 *
 * Semantics:
 *   - 'accept'   — no action needed, always proceed.
 *   - 'confirm'  — user must tap "Looks right" OR edit the label to something
 *                  non-empty and different from the NLP suggestion.
 *   - 'fallback' — picker is the ONLY authoritative correction. An
 *                  override MUST be present. The stale NLP label sitting in
 *                  the text-input state (which is not even rendered in
 *                  fallback mode) is explicitly NOT sufficient to proceed.
 *   - 'required' — reserved for the condition gate; not emitted by
 *                  classifyLocationGate, but the switch handles it
 *                  defensively.
 */
export function canProceedFromLocationGate(input: LocationProceedInput): boolean {
  // Override path: an authoritative pick from the fallback picker stands
  // on its own regardless of gate.
  if (input.hasOverride) return true;

  const userEdited = isMeaningfulLabelEdit({
    label: input.label,
    originalLabel: input.originalLabel,
  });

  switch (input.gate) {
    case 'accept':
      return true;
    case 'confirm':
      return input.confirmed || userEdited;
    case 'fallback':
      // Correction is REQUIRED and the picker is the only valid correction
      // surface — the text input is not rendered in this tier. The stale
      // NLP label in `label` state must not be treated as user intent.
      // (Without this guard, a preview with a non-empty NLP label and
      // confidence < 0.5 silently bypasses the fallback gate.)
      return false;
    case 'required':
      // Not emitted for the location gate today (kept in the union for
      // the condition gate). Treat defensively as "requires a label".
      return input.label.trim().length > 0;
  }
}
