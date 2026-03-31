/**
 * Localities API stubs — aligned to patched OpenAPI spec.
 *
 * GET /v1/localities/followed
 * PUT /v1/localities/followed  (was POST — replaces full set, max 5)
 */

import { apiFetch } from './client';

export type LocalitySummary = {
  id: string;
  wardName: string;
  cityName: string;
  countyName: string | null;
};

export type FollowedLocalitiesResponse = {
  localities: LocalitySummary[];
};

export function getFollowedLocalities(): Promise<FollowedLocalitiesResponse> {
  return apiFetch('/v1/localities/followed', { method: 'GET' });
}

export function replaceFollowedLocalities(
  localityIds: string[],
  idempotencyKey: string,
): Promise<void> {
  return apiFetch('/v1/localities/followed', {
    method: 'PUT',
    body: JSON.stringify({ localityIds }),
    idempotencyKey,
  });
}
