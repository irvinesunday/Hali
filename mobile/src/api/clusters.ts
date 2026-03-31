/**
 * Clusters API stubs — aligned to patched OpenAPI spec.
 *
 * GET  /v1/clusters
 * GET  /v1/clusters/{id}
 * POST /v1/clusters/{id}/participation    (was /participations plural, was {clusterId})
 * POST /v1/clusters/{id}/context
 * POST /v1/clusters/{id}/restoration-response
 */

import { apiFetch } from './client';

export type ClusterListParams = {
  localityId: string;
  state?: 'active' | 'possible_restoration' | 'unconfirmed' | 'resolved';
  category?: string;
};

export type ParticipationRequest = {
  type: 'affected' | 'observing' | 'no_longer_affected';
  idempotencyKey: string;
};

export type RestorationResponseRequest = {
  response: 'restoration_yes' | 'restoration_no' | 'restoration_unsure';
  idempotencyKey: string;
};

export type ContextRequest = {
  contextText: string;
};

export function getClusters(params: ClusterListParams): Promise<unknown> {
  const query = new URLSearchParams({ localityId: params.localityId });
  if (params.state) query.set('state', params.state);
  if (params.category) query.set('category', params.category);
  return apiFetch(`/v1/clusters?${query.toString()}`, { method: 'GET' });
}

export function getCluster(id: string): Promise<unknown> {
  return apiFetch(`/v1/clusters/${id}`, { method: 'GET' });
}

export function addParticipation(
  id: string,
  body: ParticipationRequest,
  idempotencyKey: string,
): Promise<void> {
  return apiFetch(`/v1/clusters/${id}/participation`, {
    method: 'POST',
    body: JSON.stringify(body),
    idempotencyKey,
  });
}

export function addContext(
  id: string,
  body: ContextRequest,
  idempotencyKey: string,
): Promise<void> {
  return apiFetch(`/v1/clusters/${id}/context`, {
    method: 'POST',
    body: JSON.stringify(body),
    idempotencyKey,
  });
}

export function submitRestorationResponse(
  id: string,
  body: RestorationResponseRequest,
  idempotencyKey: string,
): Promise<void> {
  return apiFetch(`/v1/clusters/${id}/restoration-response`, {
    method: 'POST',
    body: JSON.stringify(body),
    idempotencyKey,
  });
}
