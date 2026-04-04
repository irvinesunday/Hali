import type { ClusterResponse, OfficialPostResponse, SignalPreviewResponse } from './api';

// Re-export API types that are used directly as domain types in this MVP
export type { ClusterResponse, OfficialPostResponse };

// Signal composer state — flows through the 3-step composer
export interface ComposerState {
  freeText: string;
  locationHint?: string;
  preview: SignalPreviewResponse | null;
  // NOTE(API-MISMATCH): The OpenAPI spec includes existingClusterCandidates in
  // the preview response, but the backend SignalPreviewResponseDto only has
  // shouldSuggestJoin: bool. The composer UI uses shouldSuggestJoin to
  // determine whether to offer a "join" option in Step 3.
  idempotencyKey: string | null;
}

export interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  accountId: string | null;
  status: 'unknown' | 'authenticated' | 'unauthenticated';
}
