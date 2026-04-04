import client from './client';
import type {
  ClusterResponse,
  ClusterState,
  ParticipationRequest,
  ContextRequest,
  RestorationResponseRequest,
  HomeResponse,
} from '../types/api';

export async function getHome(localityId: string): Promise<HomeResponse> {
  const { data } = await client.get<HomeResponse>('/v1/home', {
    params: { localityId },
  });
  return data;
}

export async function getClusters(params: {
  localityId: string;
  state?: ClusterState;
  category?: string;
}): Promise<ClusterResponse[]> {
  const { data } = await client.get<ClusterResponse[]>('/v1/clusters', {
    params,
  });
  return data;
}

export async function getCluster(id: string): Promise<ClusterResponse> {
  const { data } = await client.get<ClusterResponse>(`/v1/clusters/${id}`);
  return data;
}

export async function postParticipation(
  clusterId: string,
  body: ParticipationRequest,
): Promise<void> {
  await client.post(`/v1/clusters/${clusterId}/participation`, body);
}

export async function postContext(
  clusterId: string,
  body: ContextRequest,
): Promise<void> {
  await client.post(`/v1/clusters/${clusterId}/context`, body);
}

export async function postRestorationResponse(
  clusterId: string,
  body: RestorationResponseRequest,
): Promise<void> {
  await client.post(`/v1/clusters/${clusterId}/restoration-response`, body);
}
