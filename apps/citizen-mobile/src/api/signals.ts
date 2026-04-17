// apps/citizen-mobile/src/api/signals.ts
//
// Signal intake API service layer.
// All calls route through apiRequest → Result<T, ApiError>. No throws.
// No legacy axios shim.
//
// Endpoints:
//   POST /v1/signals/preview  → single-candidate extraction + shouldSuggestJoin
//   POST /v1/signals/submit   → { signalEventId, clusterId, isNewCluster, clusterState, localityId, createdAt }

import { apiRequest } from './client';
import type {
  ApiError,
  Result,
  SignalPreviewRequest,
  SignalPreviewResponse,
  SignalSubmitRequest,
  SignalSubmitResponse,
} from '../types/api';

/**
 * POST /v1/signals/preview
 *
 * Server is [AllowAnonymous] but we send the access token anyway for
 * consistency with the rest of the composer flow (the user is always
 * authenticated when they reach the composer screen — the FAB is inside
 * the authenticated stack).
 *
 * Returns a single extracted candidate (category, subcategory, condition,
 * location, temporalType, summary) plus a boolean `shouldSuggestJoin` hint.
 * The backend does NOT return a list of existing clusters to join — that
 * join-or-create UX in earlier designs is not supported by the API.
 */
export async function previewSignal(
  body: SignalPreviewRequest,
): Promise<Result<SignalPreviewResponse, ApiError>> {
  return apiRequest<SignalPreviewResponse>('/v1/signals/preview', {
    method: 'POST',
    body: body as unknown as Record<string, unknown>,
  });
}

/**
 * POST /v1/signals/submit
 *
 * [Authorize] — access token is required. If the token has expired the
 * client's 401 interceptor will silently refresh and retry once.
 *
 * The response includes the cluster routing outcome (clusterId, isNewCluster,
 * clusterState, localityId). On success, the mobile screen navigates to the
 * cluster detail screen using the returned clusterId.
 */
export async function submitSignal(
  body: SignalSubmitRequest,
): Promise<Result<SignalSubmitResponse, ApiError>> {
  return apiRequest<SignalSubmitResponse>('/v1/signals/submit', {
    method: 'POST',
    body: body as unknown as Record<string, unknown>,
  });
}
