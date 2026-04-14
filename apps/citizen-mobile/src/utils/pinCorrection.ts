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
import type { PlaceCandidate } from '../types/api';

/**
 * Fallback coordinate when no override has been picked yet. Central-ish
 * Nairobi — Kenyan civic coverage is seeded around Nairobi county and
 * this keeps the initial pin inside the resolvable PostGIS boundary set.
 *
 * Why not seed from device GPS here? The composer does not currently
 * capture device coordinates on Step 1 (preview is sent with only
 * freeText + selectedWard + countryCode), and the pin screen is
 * deliberately permission-quiet — it must not trigger a new foreground-
 * location prompt just because the user opened it. If / when the
 * composer grows an earlier GPS capture, thread that value into
 * {@link computeInitialPinPosition} via a new explicit argument; do not
 * bolt on a silent device-GPS call here.
 */
export const DEFAULT_PIN_LATITUDE = -1.2921;
export const DEFAULT_PIN_LONGITUDE = 36.8219;

export interface InitialPinPosition {
  latitude: number;
  longitude: number;
}

/**
 * Compute the initial marker position for the pin sub-screen.
 *
 * Precedence:
 *   1. Existing override coords (refinement use-case — the user has
 *      already picked via search or "Use my current location" and is
 *      now nudging the pin on the map).
 *   2. Nairobi default (first-time users with no override yet).
 *
 * Invariants:
 *   - Return value always has finite latitude/longitude inside the
 *     global bounds. An out-of-bounds override is filtered to the
 *     default.
 *   - The returned point is purely an initial render hint; the backend
 *     still re-runs locality + spatial guards on submit, so this helper
 *     cannot itself bypass spatial integrity.
 */
export function computeInitialPinPosition(
  override: ComposerLocationOverride | null,
): InitialPinPosition {
  if (override !== null && isInBounds(override.latitude, override.longitude)) {
    return {
      latitude: override.latitude,
      longitude: override.longitude,
    };
  }

  return {
    latitude: DEFAULT_PIN_LATITUDE,
    longitude: DEFAULT_PIN_LONGITUDE,
  };
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
