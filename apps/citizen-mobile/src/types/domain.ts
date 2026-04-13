import type { ClusterResponse, OfficialPostResponse, SignalPreviewResponse } from './api';

// Re-export API types that are used directly as domain types in this MVP
export type { ClusterResponse, OfficialPostResponse };

// Signal composer state — flows through the 3-step composer
export interface ComposerState {
  freeText: string;
  locationHint?: string;
  preview: SignalPreviewResponse | null;
  idempotencyKey: string | null;
}

export interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  accountId: string | null;
  status: 'unknown' | 'authenticated' | 'unauthenticated';
}
