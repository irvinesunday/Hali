// apps/citizen-mobile/src/api/clusters.ts
//
// Cluster + home API service layer.
// All calls route through apiRequest → Result<T, ApiError>. No throws.
// No legacy axios shim.

import { apiRequest } from './client';
import { participationTypeToBackend } from '../utils/participationApi';
import type {
  ApiError,
  Result,
  ClusterResponse,
  HomeResponse,
  HomeSectionName,
  PagedSection,
  ParticipationRequest,
  ContextRequest,
  RestorationResponseRequest,
} from '../types/api';

/**
 * GET /v1/home
 *
 * Returns the full four-section home feed. The server derives the locality
 * scope from the authenticated user's follows — there is NO localityId query
 * parameter. Calling this with no follows (or unauthenticated) returns all
 * sections with items: [].
 *
 * For single-section pagination, use {@link getHomeSection} instead.
 */
export async function getHome(): Promise<Result<HomeResponse, ApiError>> {
  return apiRequest<HomeResponse>('/v1/home', { method: 'GET' });
}

/**
 * GET /v1/home?section=...&cursor=... — fetch a single paginated section.
 * Use this when paging "Load more" inside a specific section.
 * The backend returns a PagedSection<T> directly (not a HomeResponse).
 *
 * Callers should specify the item type explicitly when fetching
 * official_updates: `getHomeSection<OfficialPostResponse>(...)`.
 * Defaults to ClusterResponse for the three cluster sections.
 */
export async function getHomeSection<T = ClusterResponse>(
  section: HomeSectionName,
  cursor?: string,
): Promise<Result<PagedSection<T>, ApiError>> {
  const query: string[] = [`section=${encodeURIComponent(section)}`];
  if (cursor) {
    query.push(`cursor=${encodeURIComponent(cursor)}`);
  }
  return apiRequest<PagedSection<T>>(
    `/v1/home?${query.join('&')}`,
    { method: 'GET' },
  );
}

/**
 * GET /v1/clusters/{id}
 * Full cluster detail including linked official posts.
 * Stub here — full implementation arrives in mobile-01d (cluster detail).
 */
export async function getCluster(
  id: string,
): Promise<Result<ClusterResponse, ApiError>> {
  return apiRequest<ClusterResponse>(`/v1/clusters/${encodeURIComponent(id)}`, {
    method: 'GET',
  });
}

/**
 * POST /v1/clusters/{id}/participation
 *
 * Records an Affected / Observing / NoLongerAffected vote on a cluster.
 * Restoration votes go through submitRestorationResponse instead — the
 * backend's participation endpoint explicitly rejects restoration types.
 *
 * IMPORTANT: the backend uses Enum.TryParse against the C# ParticipationType
 * enum (case-insensitive but NOT underscore-aware), so "no_longer_affected"
 * is rejected with 422. participationTypeToBackend converts the mobile
 * snake_case union to the PascalCase wire value the controller accepts.
 */
export async function participate(
  clusterId: string,
  body: ParticipationRequest,
): Promise<Result<void, ApiError>> {
  const wireType = participationTypeToBackend(body.type);
  return apiRequest<void>(
    `/v1/clusters/${encodeURIComponent(clusterId)}/participation`,
    {
      method: 'POST',
      body: {
        type: wireType,
        deviceHash: body.deviceHash,
        idempotencyKey: body.idempotencyKey,
      },
    },
  );
}

/**
 * POST /v1/clusters/{id}/context — stub for mobile-01d.
 */
export async function addContext(
  clusterId: string,
  body: ContextRequest,
): Promise<Result<void, ApiError>> {
  return apiRequest<void>(
    `/v1/clusters/${encodeURIComponent(clusterId)}/context`,
    {
      method: 'POST',
      body: body as unknown as Record<string, unknown>,
    },
  );
}

/**
 * POST /v1/clusters/{id}/restoration-response — stub for mobile-01d.
 */
export async function submitRestorationResponse(
  clusterId: string,
  body: RestorationResponseRequest,
): Promise<Result<void, ApiError>> {
  return apiRequest<void>(
    `/v1/clusters/${encodeURIComponent(clusterId)}/restoration-response`,
    {
      method: 'POST',
      body: body as unknown as Record<string, unknown>,
    },
  );
}
