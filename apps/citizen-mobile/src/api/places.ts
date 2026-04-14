// apps/citizen-mobile/src/api/places.ts
//
// Low-confidence location fallback (C11).
// Thin wrapper around /v1/places/search and /v1/places/reverse. These
// endpoints are anonymous on the backend but the composer flow is always
// authenticated; we still send the token via apiRequest for consistency.

import { apiRequest } from './client';
import type { ApiError, PlaceCandidate, Result } from '../types/api';

/**
 * GET /v1/places/search?q=
 * Free-text place search → up to 5 candidates with coordinates + resolved
 * Hali locality. Candidates outside known localities are filtered by the
 * backend (spatial integrity), so every result is a valid submit target.
 *
 * Short queries (< 2 characters) and oversized queries (> 80) are rejected
 * by the backend with 400. Callers should debounce and trim before invoking.
 */
export async function searchPlaces(
  query: string,
): Promise<Result<PlaceCandidate[], ApiError>> {
  const q = encodeURIComponent(query.trim());
  return apiRequest<PlaceCandidate[]>(`/v1/places/search?q=${q}`, {
    method: 'GET',
  });
}

/**
 * GET /v1/places/reverse?latitude=&longitude=
 * Reverse geocode a coordinate pair to a single PlaceCandidate with a
 * human-readable label and the Hali locality the point falls inside.
 *
 * Returns 404 when the coordinates fall outside any known locality — the
 * client should fall back to place-search in that case.
 *
 * Used by the "Use my current location" action in the composer's fallback
 * picker.
 */
export async function reverseGeocodePoint(
  latitude: number,
  longitude: number,
): Promise<Result<PlaceCandidate, ApiError>> {
  const params = new URLSearchParams({
    latitude: latitude.toString(),
    longitude: longitude.toString(),
  });
  return apiRequest<PlaceCandidate>(
    `/v1/places/reverse?${params.toString()}`,
    { method: 'GET' },
  );
}
