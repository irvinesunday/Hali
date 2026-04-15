// ─── Result + Error (canonical location — do not redeclare elsewhere) ───────

/**
 * Normalised error shape surfaced by the mobile API client.
 *
 * The wire contract is the canonical backend envelope:
 *   { error: { code, message, details?, traceId } }
 *
 * `status` is the HTTP response status (0 for network errors). `code` and
 * `message` are always populated — malformed/legacy bodies degrade to
 * `code: 'unknown_error'` and a generic message. `traceId` and `details`
 * are only set when the canonical envelope provides them.
 *
 * `details` is `unknown` on purpose: today it can be a keyed field-errors
 * object (`{ fields: { fieldName: [...] } }` from `ValidationException`),
 * a string, an array, or absent. Consumers that need to read it should
 * narrow it at the use site — do not widen this type into a union here
 * (that is tracked separately; see the mobile `ErrorCode` follow-up).
 */
export interface ApiError {
  status: number;
  code: string;
  message: string;
  traceId?: string;
  details?: unknown;
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

export interface FollowedLocality {
  localityId: string;
  displayLabel: string | null;
  wardName: string;
  cityName: string | null;
}

export type FollowedLocalitiesResponse = FollowedLocality[];

export interface FollowedLocalityItem {
  localityId: string;
  displayLabel: string | null;
}

export interface SetFollowedLocalitiesBody {
  items: FollowedLocalityItem[];
}

export interface LocalitySearchResult {
  localityId: string;
  placeLabel: string;
  wardName: string;
  cityName: string | null;
}

export type LocalitySearchResponse = LocalitySearchResult[];

/**
 * Response item from GET /v1/localities/wards — the canonical full
 * ward/locality list. The mobile client caches this and runs a
 * fast client-side search/filter over it.
 */
export interface LocalitySummary {
  localityId: string;
  wardName: string;
  cityName: string | null;
}

export type LocalitiesListResponse = LocalitySummary[];

// Response from GET /v1/localities/resolve-by-coordinates.
// Shape differs from LocalitySearchResult — no placeLabel field.
export interface LocalityResolveResponse {
  localityId: string;
  wardName: string;
  cityName: string | null;
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
  | 'resolved';

/**
 * Per-caller participation snapshot returned by GET /v1/clusters/{id}.
 * Server is the source of truth for whether the two restricted CTAs may be
 * shown — the UI MUST gate on these flags rather than on local state.
 *
 * Mirrors `Hali.Contracts.Clusters.MyParticipationDto` and is null for
 * unauthenticated callers or callers with no participation row.
 */
export interface MyParticipation {
  type: ParticipationType;
  createdAt: string;
  canAddContext: boolean;
  canRespondToRestoration: boolean;
}

export interface ClusterResponse {
  id: string;
  state: ClusterState;
  category: string;
  subcategorySlug: string | null;
  title: string | null;
  summary: string | null;
  locationLabel: string | null;
  affectedCount: number;
  observingCount: number;
  createdAt: string;
  updatedAt: string;
  activatedAt: string | null;
  possibleRestorationAt: string | null;
  resolvedAt: string | null;
  officialPosts: OfficialPostResponse[];
  myParticipation: MyParticipation | null;
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

/**
 * Canonical LocationSource wire values. Must stay in sync with
 * Hali.Contracts.Signals.LocationSource (C#) and the LocationSource enum
 * in 02_openapi.yaml.
 *
 *   - 'nlp'         — CSI-NLP extraction, unchanged by the user.
 *   - 'user_edit'   — user accepted or edited the label; coords stay
 *                     device-GPS / NLP-suggested.
 *   - 'place_search'— user picked a candidate from the fallback picker
 *                     (search OR "Use my current location"); coords +
 *                     label are authoritative from the picker.
 *   - 'map_pin'     — user refined location on the C11.1 map fallback;
 *                     coords come from the marker, label from the
 *                     /v1/places/reverse response on marker release.
 */
export type SignalLocationSource =
  | 'nlp'
  | 'user_edit'
  | 'place_search'
  | 'map_pin';

export interface SignalLocation {
  areaName: string | null;
  roadName: string | null;
  junctionName: string | null;
  landmarkName: string | null;
  facilityName: string | null;
  locationLabel: string | null;
  locationPrecisionType: string | null;
  locationConfidence: number;
  // Narrowed to the canonical wire allowlist (C11). The backend
  // normalizes any unknown NLP-emitted value to 'nlp' in
  // SignalIngestionService.PreviewAsync, so this union is safe on read.
  locationSource: SignalLocationSource;
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
  /**
   * Server-authoritative flag: when true the composer MUST route the user
   * into the low-confidence fallback picker (place search / current
   * location) before allowing submit. See C11 and backend
   * LocationFallbackPolicy.
   */
  requiresLocationFallback: boolean;
}

export interface SignalSubmitRequest {
  idempotencyKey: string;
  deviceHash: string;
  freeText: string;
  category: string;
  subcategorySlug: string;
  conditionSlug?: string;
  conditionConfidence: number;
  latitude: number;
  longitude: number;
  locationLabel?: string;
  locationPrecisionType?: string;
  locationConfidence: number;
  locationSource: SignalLocationSource;
  temporalType?: string;
  neutralSummary?: string;
  sourceLanguage?: string;
}

/**
 * A single geocoding candidate returned by /v1/places/search and
 * /v1/places/reverse. Matches Hali.Contracts.Signals.PlaceCandidateDto.
 *
 * When the user selects a PlaceCandidate in the composer's fallback
 * picker, latitude/longitude become authoritative submit coordinates
 * and displayName becomes the submitted locationLabel.
 */
export interface PlaceCandidate {
  displayName: string;
  latitude: number;
  longitude: number;
  localityId: string | null;
  wardName: string | null;
  cityName: string | null;
}

/**
 * Shape of the /v1/signals/submit success response.
 * Matches Hali.Contracts.Signals.SignalSubmitResponseDto exactly.
 *
 * Clustering is synchronous — the response includes the routing outcome
 * so the client can navigate directly to the cluster detail screen.
 */
export interface SignalSubmitResponse {
  signalEventId: string;
  clusterId: string;
  isNewCluster: boolean;
  clusterState: string;
  localityId: string | null;
  createdAt: string;
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

/**
 * Restoration response wire values — verified against
 * ClustersController.cs and ParticipationService.cs.
 *
 * Backend mapping (NOT 1:1 with the ParticipationType union):
 *   "restored"       → records as ParticipationType.RestorationYes
 *   "still_affected" → records as ParticipationType.Affected (re-affirms affected)
 *   "not_sure"       → records as ParticipationType.RestorationUnsure
 *
 * There is no "restoration_no" wire value — the backend treats the
 * negative case semantically as "I'm still affected" which re-records
 * you as a current affected user.
 */
export type RestorationResponseValue = 'restored' | 'still_affected' | 'not_sure';

export interface RestorationResponseRequest {
  response: RestorationResponseValue;
  deviceHash: string;
}

// ─── Devices ──────────────────────────────────────────────────────────────────

export interface RegisterPushTokenRequest {
  expoPushToken: string;
  deviceHash: string;
}
