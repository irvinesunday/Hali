using System.Threading;
using System.Threading.Tasks;

namespace Hali.Application.Auth;

/// <summary>
/// Writes structured audit events for institution auth actions.
/// Hook point for #251 (audit trail) — a no-op implementation is used until
/// the full audit infrastructure is merged.
/// </summary>
public interface IAuthAuditService
{
    /// <summary>
    /// Records an auth audit event. Implementations must never throw — a
    /// failed audit write must not surface to the caller.
    /// </summary>
    Task LogAsync(string eventType, string? ipAddress, CancellationToken ct = default);
}

/// <summary>
/// Canonical event type constants used with <see cref="IAuthAuditService"/>.
/// </summary>
public static class AuthAuditEvents
{
    public const string MagicLinkRequested = "institution.auth.magic_link_requested";
    public const string MagicLinkVerifiedSuccess = "institution.auth.magic_link_verified.success";
    public const string MagicLinkVerifiedFailure = "institution.auth.magic_link_verified.failure";
    public const string TotpVerifiedSuccess = "institution.auth.totp_verified.success";
    public const string TotpVerifiedFailure = "institution.auth.totp_verified.failure";
    public const string SessionCreated = "institution.auth.session_created";
    public const string SessionExpired = "institution.auth.session_expired";
    public const string SessionRotated = "institution.auth.session_rotated";
    public const string Logout = "institution.auth.logout";
    public const string MagicLinkRateLimitHit = "institution.auth.rate_limit_hit";
}
