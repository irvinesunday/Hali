// ─── Result + Error (canonical location — do not redeclare elsewhere) ───────

export interface ApiError {
  status: number;
  code: string;
  message: string;
}

export type Result<T, E = ApiError> =
  | { ok: true; value: T }
  | { ok: false; error: E };

// ─── Auth ────────────────────────────────────────────────────────────────────

export interface OtpRequestBody {
  destination: string;
  authMethod: 'phone_otp' | 'email_otp' | 'magic_link';
}

export interface VerifyOtpRequestBody {
  destination: string;
  otp: string;
  deviceFingerprintHash: string;
  platform?: string;
  appVersion?: string;
  expoPushToken?: string;
}

// Shape of the /v1/auth/otp success response.
// Backend returns { "message": "OTP sent" } — no other fields.
export interface RequestOtpResponse {
  message: string;
}

// Shape of the /v1/auth/verify success response.
// Matches the backend TokenResponseDto exactly — NO accountId field.
// The account ID is extracted from the JWT's `sub` claim via
// src/utils/jwt.ts#decodeAccountIdFromJwt.
export interface VerifyOtpResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

// Legacy alias — same shape as VerifyOtpResponse. Remove when all callers
// migrate to the explicit names.
export type TokenResponse = VerifyOtpResponse;

export interface RefreshRequestBody {
  refreshToken: string;
}

// Same shape as VerifyOtpResponse — backend reuses TokenResponseDto.
export type RefreshTokenResponse = VerifyOtpResponse;

export interface LogoutRequestBody {
  refreshToken: string;
}

// ─── User ─────────────────────────────────────────────────────────────────────

export interface NotificationSettings {
  clusterActivated: boolean;
  restorationPrompt: boolean;
  clusterResolved: boolean;
}

export interface UserMeResponse {
  id: string;
  displayName: string | null;
  phoneE164: string | null;
  email: string | null;
  status: string;
  createdAt: string;
  notificationSettings: NotificationSettings;
}

// ─── Localities ───────────────────────────────────────────────────────────────

export interface FollowedLocalitiesResponse {
  localityIds: string[];
}

export interface SetFollowedLocalitiesBody {
  localityIds: string[];
}

// ─── Official Posts ───────────────────────────────────────────────────────────

export interface OfficialPostResponse {
  id: string;
  institutionId: string;
  type: string;
  category: string;
  title: string;
  body: string;
  startsAt: string | null;
  endsAt: string | null;
  status: string;
  relatedClusterId: string | null;
  isRestorationClaim: boolean;
  createdAt: string;
}

// ─── Clusters ─────────────────────────────────────────────────────────────────

export type ClusterState =
  | 'unconfirmed'
  | 'active'
  | 'possible_restoration'
  | 'resolved'
  | 'recurring_context';

export interface ClusterResponse {
  id: string;
  state: ClusterState;
  category: string;
  subcategorySlug: string | null;
  title: string | null;
  summary: string | null;
  affectedCount: number;
  observingCount: number;
  createdAt: string;
  updatedAt: string;
  activatedAt: string | null;
  possibleRestorationAt: string | null;
  resolvedAt: string | null;
  officialPosts: OfficialPostResponse[];
}

// ─── Home ─────────────────────────────────────────────────────────────────────

/**
 * Paginated section of the home feed.
 * Matches the backend `PagedSection<T>` record exactly.
 */
export interface PagedSection<T> {
  items: T[];
  nextCursor: string | null;
  totalCount: number;
}

/**
 * Home feed response. Each section is a PagedSection — NOT a flat array.
 * See Hali.Contracts/Home/HomeResponseDto.cs.
 *
 * The backend does not return an `isCalmState` flag; the client computes it
 * as: every section has `items.length === 0`.
 */
export interface HomeResponse {
  activeNow: PagedSection<ClusterResponse>;
  officialUpdates: PagedSection<OfficialPostResponse>;
  recurringAtThisTime: PagedSection<ClusterResponse>;
  otherActiveSignals: PagedSection<ClusterResponse>;
}

export type HomeSectionName =
  | 'active_now'
  | 'official_updates'
  | 'recurring_at_this_time'
  | 'other_active_signals';

// ─── Signals ──────────────────────────────────────────────────────────────────

export interface SignalLocation {
  areaName: string | null;
  roadName: string | null;
  junctionName: string | null;
  landmarkName: string | null;
  facilityName: string | null;
  locationLabel: string | null;
  locationPrecisionType: string | null;
  locationConfidence: number;
  locationSource: string;
}

export interface SignalPreviewRequest {
  freeText: string;
  userLatitude?: number;
  userLongitude?: number;
  selectedWard?: string;
  locale?: string;
  knownCity?: string;
  countryCode?: string;
}

export interface SignalPreviewResponse {
  category: string;
  subcategorySlug: string;
  conditionSlug: string | null;
  conditionConfidence: number;
  location: SignalLocation;
  temporalType: string | null;
  neutralSummary: string | null;
  shouldSuggestJoin: boolean;
}

export interface SignalSubmitRequest {
  idempotencyKey: string;
  deviceHash: string;
  freeText: string;
  category: string;
  subcategorySlug: string;
  conditionSlug?: string;
  conditionConfidence: number;
  latitude?: number;
  longitude?: number;
  locationLabel?: string;
  locationPrecisionType?: string;
  locationConfidence: number;
  locationSource: string;
  temporalType?: string;
  neutralSummary?: string;
  sourceLanguage?: string;
  spatialCellId?: string;
}

export interface SignalSubmitResponse {
  clusterId: string;
  clusterState: string;
  isNewCluster: boolean;
}

// ─── Participation ────────────────────────────────────────────────────────────

export type ParticipationType =
  | 'affected'
  | 'observing'
  | 'no_longer_affected'
  | 'restoration_yes'
  | 'restoration_no'
  | 'restoration_unsure';

export interface ParticipationRequest {
  type: ParticipationType;
  deviceHash: string;
  idempotencyKey?: string;
}

export interface ContextRequest {
  text: string;
  deviceHash: string;
}

export interface RestorationResponseRequest {
  response: 'restoration_yes' | 'restoration_no' | 'restoration_unsure';
  deviceHash: string;
}

// ─── Devices ──────────────────────────────────────────────────────────────────

export interface RegisterPushTokenRequest {
  expoPushToken: string;
  deviceHash: string;
}
