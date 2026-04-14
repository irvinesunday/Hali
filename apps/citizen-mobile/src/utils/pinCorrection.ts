// apps/citizen-mobile/src/utils/pinCorrection.ts
//
// Pure helpers for the C11.1 draggable map-pin correction flow.
//
// Extracted from the pin sub-screen so the selection logic (where the
// initial marker lands, what a "valid" reverse-geocode response looks
// like) is unit-testable without loading React Native or the Maps module.

import type {
  ComposerLocationOverride,
  LocationOverridePickedVia,
} from '../context/ComposerContext';
import type { PlaceCandidate, SignalPreviewResponse } from '../types/api';

/**
 * Fallback coordinate when no context is available. Central-ish Nairobi —
 * Kenyan civic coverage is seeded around Nairobi county and this keeps
 * the initial pin inside the resolvable PostGIS boundary set. Callers
 * that have *any* stronger signal (existing override / preview context)
 * MUST use it instead; this constant is the last-resort origin for a
 * first-time user with no GPS and no prior pick.
 */
export const DEFAULT_PIN_LATITUDE = -1.2921;
export const DEFAULT_PIN_LONGITUDE = 36.8219;

export interface InitialPinPositionInput {
  /**
   * Current composer override, if the user has already picked via search
   * or "Use my current location". Preferred starting point because the
   * map is a *refinement* surface for an existing pick.
   */
  override: ComposerLocationOverride | null;
  /**
   * The preview response's user-supplied lat/lng (from Step 1) when the
   * user sent their current GPS with the preview call. Used when no
   * override has been picked yet.
   */
  previewUserLatitude?: number | null;
  previewUserLongitude?: number | null;
}

export interface InitialPinPosition {
  latitude: number;
  longitude: number;
}

/**
 * Compute the initial marker position for the pin sub-screen.
 *
 * Precedence:
 *   1. Existing override coords (refinement use-case).
 *   2. Preview's userLatitude / userLongitude (the composer already had a
 *      fix when the user hit Preview).
 *   3. Nairobi default (guarantees a valid-looking starting point even
 *      on first-time users with no GPS).
 *
 * Invariants:
 *   - Return value always has finite latitude/longitude inside the
 *     global bounds. Out-of-bounds inputs are filtered to the default.
 *   - The returned point is purely an initial render hint; the backend
 *     still re-runs locality + spatial guards on submit, so this helper
 *     cannot itself bypass spatial integrity.
 */
export function computeInitialPinPosition(
  input: InitialPinPositionInput,
): InitialPinPosition {
  if (input.override !== null && isInBounds(input.override.latitude, input.override.longitude)) {
    return {
      latitude: input.override.latitude,
      longitude: input.override.longitude,
    };
  }

  const pLat = input.previewUserLatitude;
  const pLng = input.previewUserLongitude;
  if (
    typeof pLat === 'number' &&
    typeof pLng === 'number' &&
    isInBounds(pLat, pLng)
  ) {
    return { latitude: pLat, longitude: pLng };
  }

  return {
    latitude: DEFAULT_PIN_LATITUDE,
    longitude: DEFAULT_PIN_LONGITUDE,
  };
}

/**
 * Narrow variant that picks the initial pin from a {@link SignalPreviewResponse}
 * + override pair. Thin adapter so the sub-screen doesn't need to destructure
 * the preview at the call site.
 */
export function computeInitialPinFromContext(
  override: ComposerLocationOverride | null,
  preview: SignalPreviewResponse | null,
  previewUserLatitude?: number | null,
  previewUserLongitude?: number | null,
): InitialPinPosition {
  return computeInitialPinPosition({
    override,
    previewUserLatitude: previewUserLatitude ?? null,
    previewUserLongitude: previewUserLongitude ?? null,
  });
  // `preview` currently unused in the body — kept on the signature because
  // future refinements may read NLP-extracted precision hints out of it
  // to widen / tighten the map's initial zoom. Leaving it here now keeps
  // the call-site stable.
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  void preview;
}

/**
 * Build a {@link ComposerLocationOverride} from a reverse-geocode result
 * on marker release. Returns null when the PlaceCandidate is missing a
 * non-empty displayName — we refuse to submit a picker pick without an
 * authoritative human-readable label (mirrors the backend's
 * validation.location_label_required guard).
 *
 * The reverse-geocode itself is performed by {@link reverseGeocodePoint}
 * (src/api/places.ts). The backend enforces locality validation there
 * and returns 404 for points outside known coverage, so a null from
 * this helper can also reflect an outside-locality pin.
 */
export function overrideFromMapPin(
  candidate: PlaceCandidate,
): ComposerLocationOverride | null {
  if (!candidate.displayName || candidate.displayName.trim().length === 0) {
    return null;
  }
  if (!isInBounds(candidate.latitude, candidate.longitude)) {
    return null;
  }
  return {
    latitude: candidate.latitude,
    longitude: candidate.longitude,
    label: candidate.displayName,
    source: 'map_pin',
  };
}

/**
 * UI-only path flag for the fallback picker's selected-state subtitle.
 * The map path reports its provenance as 'map' so the user sees
 * "Dropped pin" instead of the "Selected from place search" copy.
 */
export const MAP_PICKED_VIA: LocationOverridePickedVia = 'map';

function isInBounds(lat: number, lng: number): boolean {
  return (
    Number.isFinite(lat) &&
    Number.isFinite(lng) &&
    lat >= -90 &&
    lat <= 90 &&
    lng >= -180 &&
    lng <= 180
  );
}
