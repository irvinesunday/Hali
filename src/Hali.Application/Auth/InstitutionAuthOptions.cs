namespace Hali.Application.Auth;

/// <summary>
/// Configuration binding for the Phase 2 institution web auth + session
/// hardening (#197). Populated from the <c>InstitutionAuth</c> configuration
/// section. Defaults match the security posture in
/// <c>docs/arch/SECURITY_POSTURE.md §3</c> and
/// <c>docs/arch/hali_institution_ux_layout_spec.md §8.5</c>.
/// </summary>
public class InstitutionAuthOptions
{
    /// <summary>
    /// Idle timeout. A session whose <c>last_activity_at</c> is older than
    /// this is treated as expired on the next request. Default 30 minutes.
    /// </summary>
    public int SessionIdleMinutes { get; set; } = 30;

    /// <summary>
    /// Soft warning threshold. The client surfaces a "session about to end"
    /// banner when the idle age reaches this value. Server enforces the
    /// hard idle timeout regardless. Default 27 minutes.
    /// </summary>
    public int SessionSoftWarningMinutes { get; set; } = 27;

    /// <summary>
    /// Absolute session lifetime. A session that reaches this age is
    /// terminated regardless of recent activity. Default 12 hours.
    /// </summary>
    public int SessionAbsoluteHours { get; set; } = 12;

    /// <summary>Magic-link token validity. Default 15 minutes.</summary>
    public int MagicLinkTtlMinutes { get; set; } = 15;

    /// <summary>
    /// Step-up-auth window. A privileged action is allowed when
    /// <c>step_up_verified_at</c> falls inside this window. Default 5 minutes.
    /// </summary>
    public int StepUpWindowMinutes { get; set; } = 5;

    /// <summary>
    /// Number of TOTP recovery codes emitted on enrollment. Default 10.
    /// </summary>
    public int RecoveryCodeCount { get; set; } = 10;

    /// <summary>
    /// Issuer identity shown in authenticator apps (<c>otpauth://</c> URI).
    /// </summary>
    public string TotpIssuer { get; set; } = "Hali";

    /// <summary>Name of the session cookie.</summary>
    public string SessionCookieName { get; set; } = "hali_institution_session";

    /// <summary>Name of the CSRF token cookie.</summary>
    public string CsrfCookieName { get; set; } = "hali_institution_csrf";

    /// <summary>
    /// When <c>false</c>, the session cookie is issued without the
    /// <c>Secure</c> flag so local HTTP testing works. Production sets this
    /// to <c>true</c> — never softened via hard-coded defaults.
    /// </summary>
    public bool RequireSecureCookies { get; set; } = true;
}
