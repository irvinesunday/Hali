using System.Collections.Generic;

namespace Hali.Application.Errors;

/// <summary>
/// Canonical catalog of stable wire error codes used by the Hali API.
///
/// Every <see cref="AppException"/> subclass thrown from application/controller
/// code MUST pass one of these constants as its <c>code</c> argument, never a
/// bare string literal. This is enforced by
/// <c>Hali.Tests.Unit.Errors.ErrorCodeCatalogTests</c>.
///
/// Codes follow the <c>namespace.reason</c> snake_case convention established
/// by H1 (#106) and H2 (#134). The <see cref="InternalOnlyCodes"/> set lists
/// codes that appear in logs/traces for debuggability but are redacted to
/// <see cref="ServerInternalError"/> before reaching the wire.
/// </summary>
public static class ErrorCodes
{
    // --- account.* ---
    public const string AccountNotFound = "account.not_found";

    // --- auth.* ---
    /// <summary>
    /// Emitted by the JwtBearer challenge handler for missing/invalid/expired
    /// tokens on <c>[Authorize]</c> endpoints. No controller throws this
    /// directly — it is produced by the inline
    /// <c>JwtBearerEvents.OnChallenge</c> handler configured in
    /// <c>src/Hali.Api/Program.cs</c>.
    /// </summary>
    public const string AuthUnauthenticated = "auth.unauthenticated";
    public const string AuthUnauthorized = "auth.unauthorized";
    public const string AuthForbidden = "auth.forbidden";
    /// <summary>
    /// Emitted by the JwtBearer forbidden handler when an authenticated caller
    /// fails a role-gated <c>[Authorize(Roles = ...)]</c> check. Distinct from
    /// the application-layer <see cref="AuthForbidden"/> (thrown by controller
    /// code when a permission is denied in the logic path).
    /// </summary>
    public const string AuthRoleInsufficient = "auth.role_insufficient";
    public const string AuthInstitutionIdMissing = "auth.institution_id_missing";
    public const string AuthOtpInvalid = "auth.otp_invalid";
    public const string AuthOtpRateLimited = "auth.otp_rate_limited";
    public const string AuthRefreshTokenInvalid = "auth.refresh_token_invalid";
    // Phase 2 institution auth + session hardening (#197).
    public const string AuthInstitutionSessionInvalid = "auth.institution_session_invalid";
    public const string AuthInstitutionSessionIdleTimeout = "auth.institution_session_idle_timeout";
    public const string AuthInstitutionSessionAbsoluteTimeout = "auth.institution_session_absolute_timeout";
    public const string AuthCsrfMissing = "auth.csrf_missing";
    public const string AuthCsrfMismatch = "auth.csrf_mismatch";
    public const string AuthMagicLinkInvalid = "auth.magic_link_invalid";
    public const string AuthTotpAlreadyEnrolled = "auth.totp_already_enrolled";
    public const string AuthTotpNotEnrolled = "auth.totp_not_enrolled";
    public const string AuthTotpInvalidCode = "auth.totp_invalid_code";
    public const string AuthTotpNotConfirmed = "auth.totp_not_confirmed";
    public const string AuthStepUpRequired = "auth.step_up_required";

    // --- institution_admin.* --- Phase 2 institution-admin routes (#196).
    /// <summary>
    /// An institution_admin attempted to elevate another account to the
    /// institution_admin role. Self-service elevation is blocked until
    /// the approval flow lands (deferred by design — see #196 scope).
    /// </summary>
    public const string InstitutionAdminElevationRequiresApproval = "institution_admin.elevation_requires_approval";
    /// <summary>
    /// An institution_admin attempted to demote themselves while they
    /// are the last institution_admin for their institution — guards
    /// against a lockout where nobody can manage the institution.
    /// </summary>
    public const string InstitutionAdminLastAdminCannotDemote = "institution_admin.last_admin_cannot_demote";
    /// <summary>
    /// A user-management request targeted a userId outside the acting
    /// admin's institution. Returned as 404 to prevent cross-institution
    /// user-existence probing.
    /// </summary>
    public const string InstitutionAdminUserNotFound = "institution_admin.user_not_found";
    /// <summary>
    /// An invite destination email is already bound to an existing
    /// account (either in this institution or another).
    /// </summary>
    public const string InstitutionAdminEmailAlreadyInUse = "institution_admin.email_already_in_use";
    /// <summary>
    /// The institution referenced by the acting admin's JWT/session
    /// claim no longer exists (orphan claim). Invariant violation — if
    /// this fires the deployment has drifted (institution deleted but
    /// sessions still live). Returned as 404 with a distinct code so
    /// operators can tell it apart from a user-not-found event.
    /// </summary>
    public const string InstitutionAdminInstitutionNotFound = "institution_admin.institution_not_found";

    // --- cluster.* ---
    public const string ClusterNotFound = "cluster.not_found";

    // --- dependency.* ---
    public const string DependencyNlpUnavailable = "dependency.nlp_unavailable";
    public const string DependencySpatialDerivationFailed = "dependency.spatial_derivation_failed";

    // --- device.* ---
    public const string DeviceMissingFields = "device.missing_fields";
    public const string DeviceNotFound = "device.not_found";

    // --- institution.* ---
    public const string InstitutionMissingFields = "institution.missing_fields";
    /// <summary>
    /// The <c>state</c> filter supplied on <c>GET /v1/institution/clusters</c>
    /// was not one of the canonical values (<c>active</c>, <c>growing</c>,
    /// <c>needs_attention</c>, <c>restoration</c>).
    /// </summary>
    public const string InstitutionInvalidStateFilter = "institution.invalid_state_filter";
    /// <summary>
    /// <c>POST /v1/institution/clusters/{clusterId}/acknowledge</c> was issued
    /// for a cluster outside the caller's jurisdiction. Returned as 404
    /// (not 403) to deny the existence probe — institution A must not be
    /// able to confirm cluster ids owned by institution B.
    /// </summary>
    public const string InstitutionAcknowledgeOutOfScope = "institution.acknowledge_out_of_scope";
    /// <summary>
    /// <c>POST /v1/institution/clusters/{clusterId}/acknowledge</c> was issued
    /// without an <c>idempotencyKey</c> — required per the mutation-endpoint
    /// rule in <c>docs/arch/02_api_contracts.md</c> §Idempotency.
    /// </summary>
    public const string InstitutionAcknowledgeMissingIdempotencyKey = "institution.acknowledge_missing_idempotency_key";

    // --- invite.* ---
    public const string InviteAlreadyAccepted = "invite.already_accepted";
    public const string InviteExpired = "invite.expired";
    public const string InviteInvalid = "invite.invalid";

    // --- locality.* ---
    public const string LocalityNotFound = "locality.not_found";
    public const string LocalityQueryTooLong = "locality.query_too_long";
    public const string LocalityQueryTooShort = "locality.query_too_short";
    public const string LocalitySearchRateLimited = "locality.search_rate_limited";

    // --- official_post.* ---
    public const string OfficialPostInvalidCategory = "official_post.invalid_category";
    public const string OfficialPostInvalidType = "official_post.invalid_type";
    public const string OfficialPostMissingFields = "official_post.missing_fields";
    public const string OfficialPostOutsideJurisdiction = "official_post.outside_jurisdiction";
    /// <summary>
    /// <c>response_status</c> on a <c>live_update</c> post was outside the
    /// canonical set or was supplied on a non-live_update post.
    /// </summary>
    public const string OfficialPostInvalidResponseStatus = "official_post.invalid_response_status";
    /// <summary>
    /// <c>severity</c> on a <c>scheduled_disruption</c> post was outside the
    /// canonical set or was supplied on a non-scheduled_disruption post.
    /// </summary>
    public const string OfficialPostInvalidSeverity = "official_post.invalid_severity";

    // --- participation.* ---
    public const string ParticipationContextRequiresAffected = "participation.context_requires_affected";
    public const string ParticipationContextWindowExpired = "participation.context_window_expired";
    public const string ParticipationRestorationRequiresAffected = "participation.restoration_requires_affected";

    // --- places.* ---
    public const string PlacesQueryTooLong = "places.query_too_long";
    public const string PlacesQueryTooShort = "places.query_too_short";
    public const string PlacesReverseRateLimited = "places.reverse_rate_limited";
    public const string PlacesSearchRateLimited = "places.search_rate_limited";

    // --- rate_limit.* ---
    /// <summary>
    /// Default code for <see cref="RateLimitException"/>. Replaces the pre-H3
    /// <c>integrity.rate_limited</c> code; the rename is documented in the
    /// PR for #153.
    /// </summary>
    public const string RateLimitExceeded = "rate_limit.exceeded";

    // --- server.* ---
    /// <summary>
    /// Generic 500 emitted by <c>ExceptionToApiErrorMapper</c> for unmapped
    /// exceptions and for typed <see cref="AppException"/>s carrying
    /// <c>ErrorCategory.Unexpected</c> (internal-invariant violations).
    /// </summary>
    public const string ServerInternalError = "server.internal_error";

    // --- signal.* ---
    public const string SignalDuplicate = "signal.duplicate";

    // --- validation.* ---
    /// <summary>
    /// Default code for <see cref="ValidationException"/>. Kept for
    /// back-compat; call sites should prefer a more specific code
    /// (e.g. <see cref="ValidationMissingField"/>) whenever the concept is
    /// discriminable.
    /// </summary>
    public const string ValidationFailed = "validation.failed";
    public const string ValidationDeviceNotFound = "validation.device_not_found";
    public const string ValidationInvalidCategory = "validation.invalid_category";
    public const string ValidationInvalidCoordinates = "validation.invalid_coordinates";
    public const string ValidationInvalidLocationSource = "validation.invalid_location_source";
    public const string ValidationInvalidParticipationType = "validation.invalid_participation_type";
    public const string ValidationInvalidRestorationResponse = "validation.invalid_restoration_response";
    public const string ValidationInvalidSection = "validation.invalid_section";
    public const string ValidationLocalityUnresolved = "validation.locality_unresolved";
    public const string ValidationLocationLabelRequired = "validation.location_label_required";
    public const string ValidationLocationLabelTooLong = "validation.location_label_too_long";
    public const string ValidationMaxFollowedLocalitiesExceeded = "validation.max_followed_localities_exceeded";
    public const string ValidationMissingCoordinates = "validation.missing_coordinates";
    /// <summary>
    /// Discriminable code for "a required field was absent or blank". Replaces
    /// generic <see cref="ValidationFailed"/> in missing-field sites so mobile
    /// can branch on the code rather than parsing messages.
    /// </summary>
    public const string ValidationMissingField = "validation.missing_field";

    // --- Internal-only (never on the wire) ---
    /// <summary>
    /// Signal reached the clustering pipeline without a derived spatial cell —
    /// an internal invariant violation, not a client-visible error. Redacted
    /// by <see cref="Hali.Api.Errors.ExceptionToApiErrorMapper"/> to
    /// <see cref="ServerInternalError"/> before reaching the response body.
    /// </summary>
    public const string ClusteringNoSpatialCell = "clustering.no_spatial_cell";

    /// <summary>
    /// Codes that are thrown with typed exceptions for internal
    /// log/trace correlation but must never be surfaced on the HTTP wire.
    /// The exception mapper redacts these to <see cref="ServerInternalError"/>
    /// via <see cref="Hali.Domain.Errors.ErrorCategory.Unexpected"/>; the
    /// OpenAPI <c>ErrorCode</c> enum does not include them.
    /// </summary>
    public static readonly IReadOnlySet<string> InternalOnlyCodes = new HashSet<string>
    {
        ClusteringNoSpatialCell,
    };
}
