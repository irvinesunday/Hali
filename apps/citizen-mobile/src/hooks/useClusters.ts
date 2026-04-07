// apps/citizen-mobile/src/hooks/useClusters.ts
//
// TanStack Query bridge for the cluster / home API service.
// Services return Result<T, ApiError> — React Query wants throws — so each
// hook unwraps the Result and throws an Error with the ApiError attached.
// This keeps service tests easy (pure Result<>) and React Query UX normal
// (isLoading / isError / error flows through the query).

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  getHome,
  getCluster,
  participate,
  addContext,
  submitRestorationResponse,
} from '../api/clusters';
import type {
  ApiError,
  HomeResponse,
  ClusterResponse,
  ParticipationRequest,
  ContextRequest,
  RestorationResponseRequest,
  Result,
} from '../types/api';

/**
 * Error thrown by bridge hooks when a service Result is !ok.
 * Consumers can access the structured ApiError via err.apiError.
 */
export class ApiResultError extends Error {
  constructor(public readonly apiError: ApiError) {
    super(apiError.message);
    this.name = 'ApiResultError';
  }
}

function unwrap<T>(result: Result<T, ApiError>): T {
  if (result.ok) return result.value;
  throw new ApiResultError(result.error);
}

export function useHome() {
  return useQuery<HomeResponse, ApiResultError>({
    queryKey: ['home'],
    queryFn: async () => unwrap(await getHome()),
    staleTime: 30_000,
  });
}

export function useCluster(id: string) {
  return useQuery<ClusterResponse, ApiResultError>({
    queryKey: ['cluster', id],
    queryFn: async () => unwrap(await getCluster(id)),
    enabled: Boolean(id),
    staleTime: 15_000,
  });
}

export function useParticipation(clusterId: string) {
  const qc = useQueryClient();
  return useMutation<void, ApiResultError, ParticipationRequest>({
    mutationFn: async (body) => unwrap(await participate(clusterId, body)),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['cluster', clusterId] });
      qc.invalidateQueries({ queryKey: ['home'] });
    },
  });
}

export function useAddContext(clusterId: string) {
  const qc = useQueryClient();
  return useMutation<void, ApiResultError, ContextRequest>({
    mutationFn: async (body) => unwrap(await addContext(clusterId, body)),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['cluster', clusterId] });
    },
  });
}

export function useRestorationResponse(clusterId: string) {
  const qc = useQueryClient();
  return useMutation<void, ApiResultError, RestorationResponseRequest>({
    mutationFn: async (body) =>
      unwrap(await submitRestorationResponse(clusterId, body)),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['cluster', clusterId] });
      qc.invalidateQueries({ queryKey: ['home'] });
    },
  });
}
