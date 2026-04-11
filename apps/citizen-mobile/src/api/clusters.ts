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
  OfficialPostResponse,
  HomeResponse,
  HomeSectionName,
  PagedSection,
  ParticipationRequest,
  ContextRequest,
  RestorationResponseRequest,
} from '../types/api';

/** Maps each home section name to its item type on the wire. */
type SectionItemType<S extends HomeSectionName> =
  S extends 'official_updates' ? OfficialPostResponse : ClusterResponse;

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
 * The item type is inferred from the section name:
 *   - 'official_updates' → OfficialPostResponse
 *   - all others → ClusterResponse
 */
export async function getHomeSection<S extends HomeSectionName>(
  section: S,
  cursor?: string,
): Promise<Result<PagedSection<SectionItemType<S>>, ApiError>> {
  const query: string[] = [`section=${encodeURIComponent(section)}`];
  if (cursor) {
    query.push(`cursor=${encodeURIComponent(cursor)}`);
  }
  return apiRequest<PagedSection<SectionItemType<S>>>(
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
