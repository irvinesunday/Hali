// apps/citizen-mobile/src/api/localities.ts
//
// Followed localities (wards) service.
// Result<T, ApiError> — no throws, no axios shim.

import { apiRequest } from './client';
import type {
  ApiError,
  FollowedLocalitiesResponse,
  Result,
  SetFollowedLocalitiesBody,
} from '../types/api';

/**
 * GET /v1/localities/followed
 * Returns the current user's followed ward IDs (max 5).
 * 401 if unauthenticated.
 */
export async function getFollowedLocalities(): Promise<
  Result<FollowedLocalitiesResponse, ApiError>
> {
  return apiRequest<FollowedLocalitiesResponse>('/v1/localities/followed', {
    method: 'GET',
  });
}

/**
 * PUT /v1/localities/followed
 * Replaces the full set of followed localities.
 * Returns 204 on success.
 * Returns 422 with code 'max_followed_localities_exceeded' over the limit.
 */
export async function setFollowedLocalities(
  body: SetFollowedLocalitiesBody,
): Promise<Result<void, ApiError>> {
  return apiRequest<void>('/v1/localities/followed', {
    method: 'PUT',
    body: body as unknown as Record<string, unknown>,
  });
}
