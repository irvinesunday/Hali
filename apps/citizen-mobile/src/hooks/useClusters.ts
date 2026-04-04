import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { getHome, getCluster, postParticipation, postContext, postRestorationResponse } from '../api/clusters';
import type { ParticipationRequest, ContextRequest, RestorationResponseRequest } from '../types/api';

export function useHome(localityId: string | null) {
  return useQuery({
    queryKey: ['home', localityId],
    queryFn: () => getHome(localityId!),
    enabled: !!localityId,
    staleTime: 30_000,
  });
}

export function useCluster(id: string) {
  return useQuery({
    queryKey: ['cluster', id],
    queryFn: () => getCluster(id),
    staleTime: 15_000,
  });
}

export function useParticipation(clusterId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: ParticipationRequest) =>
      postParticipation(clusterId, body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['cluster', clusterId] });
      qc.invalidateQueries({ queryKey: ['home'] });
    },
  });
}

export function useAddContext(clusterId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: ContextRequest) => postContext(clusterId, body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['cluster', clusterId] });
    },
  });
}

export function useRestorationResponse(clusterId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: RestorationResponseRequest) =>
      postRestorationResponse(clusterId, body),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['cluster', clusterId] });
      qc.invalidateQueries({ queryKey: ['home'] });
    },
  });
}
