using System;

namespace Hali.Domain.Entities.Auth;

/// <summary>
/// Institution-web and institution-admin web session. Holds the server-side
/// state behind the <c>hali_institution_session</c> cookie — the cookie
/// itself carries only an opaque token whose SHA-256 hash is stored here.
/// </summary>
public class WebSession
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    /// <summary>
    /// Institution the session is scoped to. Null for admin users that span
    /// multiple institutions (Hali-ops — not yet in scope).
    /// </summary>
    public Guid? InstitutionId { get; set; }

    /// <summary>
    /// SHA-256 hex of the session token carried by the cookie. Never the
    /// plaintext — cookie theft remains recoverable via revocation.
    /// </summary>
    public string SessionTokenHash { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hex of the CSRF token delivered alongside the session. The
    /// plaintext is mirrored into the <c>hali_institution_csrf</c> cookie
    /// (double-submit pattern). Rotated on session creation only.
    /// </summary>
    public string CsrfTokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Last request timestamp. Drives the 30-minute idle timeout enforced
    /// by <c>InstitutionSessionMiddleware</c>.
    /// </summary>
    public DateTime LastActivityAt { get; set; }

    /// <summary>
    /// Hard cap: <c>CreatedAt + 12 hours</c>. A session that reaches this
    /// timestamp is terminated regardless of activity.
    /// </summary>
    public DateTime AbsoluteExpiresAt { get; set; }

    /// <summary>
    /// Last fresh TOTP verification. Step-up-auth-gated actions require
    /// this to fall inside the configured step-up window.
    /// </summary>
    public DateTime? StepUpVerifiedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Role snapshotted at session creation. Values: <c>institution</c>,
    /// <c>institution_admin</c>. The middleware emits this value as the
    /// role claim on every request, so a change to the account's
    /// <c>is_institution_admin</c> flag only takes effect on next login.
    /// This bounds privilege escalation / de-escalation to the session's
    /// 12 h absolute lifetime.
    /// </summary>
    public string Role { get; set; } = "institution";
}
