// apps/citizen-mobile/src/api/clusters.ts
//
// Cluster + home API service layer.
// All calls route through apiRequest → Result<T, ApiError>. No throws.
// No legacy axios shim.

import { apiRequest } from './client';
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
 * Pass a `section` name to fetch only that section (paginated via cursor).
 */
export async function getHome(
  options: { section?: HomeSectionName; cursor?: string } = {},
): Promise<Result<HomeResponse, ApiError>> {
  const query: string[] = [];
  if (options.section) {
    query.push(`section=${encodeURIComponent(options.section)}`);
  }
  if (options.cursor) {
    query.push(`cursor=${encodeURIComponent(options.cursor)}`);
  }
  const path = query.length > 0 ? `/v1/home?${query.join('&')}` : '/v1/home';
  return apiRequest<HomeResponse>(path, { method: 'GET' });
}

/**
 * GET /v1/home?section=...&cursor=... — fetch a single paginated section.
 * Use this when paging "Load more" inside a specific section.
 * The backend returns a PagedSection<T> wrapper directly (not a HomeResponse).
 */
export async function getHomeSection(
  section: HomeSectionName,
  cursor?: string,
): Promise<Result<PagedSection<ClusterResponse>, ApiError>> {
  const query: string[] = [`section=${encodeURIComponent(section)}`];
  if (cursor) {
    query.push(`cursor=${encodeURIComponent(cursor)}`);
  }
  return apiRequest<PagedSection<ClusterResponse>>(
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
 * POST /v1/clusters/{id}/participation — stub for mobile-01d.
 */
export async function participate(
  clusterId: string,
  body: ParticipationRequest,
): Promise<Result<void, ApiError>> {
  return apiRequest<void>(
    `/v1/clusters/${encodeURIComponent(clusterId)}/participation`,
    {
      method: 'POST',
      body: body as unknown as Record<string, unknown>,
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
