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

export interface RequestOtpResponse {
  message: string;
  expiresInSeconds?: number;
}

export interface VerifyOtpResponse {
  accessToken: string;
  refreshToken: string;
  accountId: string;
  expiresIn: number;
}

// Kept for legacy imports — alias to VerifyOtpResponse.
// Remove once all callers migrate to VerifyOtpResponse.
export type TokenResponse = VerifyOtpResponse;

export interface RefreshRequestBody {
  refreshToken: string;
}

export interface RefreshTokenResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

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

export interface HomeResponse {
  activeNow: ClusterResponse[];
  officialUpdates: OfficialPostResponse[];
  recurringAtThisTime: ClusterResponse[];
  otherActiveSignals: ClusterResponse[];
}

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
