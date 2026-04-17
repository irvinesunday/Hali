using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Auth;
using Hali.Application.Errors;
using Hali.Contracts.Auth;
using Hali.Domain.Entities.Auth;
using Hali.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hali.Api.Controllers;

/// <summary>
/// Institution-web auth endpoints (#197). Implements the magic-link +
/// TOTP + web-session lifecycle. All cookie management (issue, refresh,
/// clear) lives here; the middleware only validates.
/// </summary>
[ApiController]
[Route("v1/auth/institution")]
// Class-level role gate. Without a role constraint, a citizen JWT would
// satisfy bare [Authorize] and reach the TOTP / session endpoints — the
// institution session cookie + the citizen JWT are both "authenticated"
// principals but only the institution surface should reach these routes.
// Magic-link request + verify are [AllowAnonymous] below, which overrides
// the class-level gate for the pre-session endpoints.
[Authorize(Roles = "institution")]
public sealed class InstitutionAuthController : ControllerBase
{
    private readonly IMagicLinkService _magicLink;
    private readonly ITotpService _totp;
    private readonly IInstitutionSessionService _sessions;
    private readonly IInstitutionAuthRepository _repo;
    private readonly IAuthRepository _authRepo;
    private readonly InstitutionAuthOptions _opts;

    public InstitutionAuthController(
        IMagicLinkService magicLink,
        ITotpService totp,
        IInstitutionSessionService sessions,
        IInstitutionAuthRepository repo,
        IAuthRepository authRepo,
        IOptions<InstitutionAuthOptions> options)
    {
        _magicLink = magicLink;
        _totp = totp;
        _sessions = sessions;
        _repo = repo;
        _authRepo = authRepo;
        _opts = options.Value;
    }

    // ---- Magic link ---------------------------------------------------

    [HttpPost("magic-link/request")]
    [AllowAnonymous]
    public async Task<ActionResult<MagicLinkRequestResponseDto>> RequestMagicLink(
        [FromBody] MagicLinkRequestDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            throw ValidationFromModelState();
        }

        MagicLinkIssued issued = await _magicLink.IssueAsync(dto.Email, ct);

        // Deliberate UX: the response body never indicates whether the
        // email was registered. Response shape is identical for known
        // and unknown addresses to prevent account enumeration.
        return Ok(new MagicLinkRequestResponseDto(
            Message: "If the email matches a registered institution user, a magic link has been sent.",
            ExpiresAt: issued.ExpiresAt));
    }

    [HttpPost("magic-link/verify")]
    [AllowAnonymous]
    public async Task<ActionResult<MagicLinkVerifyResponseDto>> VerifyMagicLink(
        [FromBody] MagicLinkVerifyRequestDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            throw ValidationFromModelState();
        }

        MagicLinkToken? token = await _magicLink.ConsumeAsync(dto.Token, ct);
        if (token is null || token.AccountId is null)
        {
            throw new UnauthorizedException(
                code: ErrorCodes.AuthMagicLinkInvalid,
                message: "Invalid or expired magic link.");
        }

        Account? account = await _authRepo.FindAccountByIdAsync(token.AccountId.Value, ct);
        if (account is null || account.AccountType == AccountType.Citizen)
        {
            // A citizen account cannot assume an institution session even
            // if a magic link was mis-issued. Surface as "invalid" to
            // avoid leaking account-type information.
            throw new UnauthorizedException(
                code: ErrorCodes.AuthMagicLinkInvalid,
                message: "Invalid or expired magic link.");
        }

        // Role snapshotted at session creation — institution_admin when
        // the account carries the per-institution admin flag, otherwise
        // the bare institution role.
        string role = account.IsInstitutionAdmin ? "institution_admin" : "institution";
        SessionCreated created = await _sessions.CreateAsync(
            account.Id, account.InstitutionId, role, ct);

        IssueSessionCookies(created);

        // Signal the client whether TOTP enrollment is still needed.
        // Confirmed secret → client should call /totp/verify next.
        // Unconfirmed or missing → client should call /totp/enroll next.
        TotpSecret? totp = await _repo.FindTotpSecretByAccountAsync(account.Id, ct);
        bool requiresEnrollment = totp is null || totp.ConfirmedAt is null;
        bool requiresVerification = !requiresEnrollment;

        return Ok(new MagicLinkVerifyResponseDto(
            RequiresTotpEnrollment: requiresEnrollment,
            RequiresTotpVerification: requiresVerification,
            Session: BuildSessionBody(created.Session)));
    }

    // ---- TOTP ---------------------------------------------------------

    [HttpPost("totp/enroll")]
    [Authorize]
    public async Task<ActionResult<TotpEnrollResponseDto>> EnrollTotp(CancellationToken ct)
    {
        Guid accountId = ResolveAccountId();
        Account? account = await _authRepo.FindAccountByIdAsync(accountId, ct);
        if (account is null)
        {
            throw new UnauthorizedException(ErrorCodes.AuthUnauthorized, "Account not found.");
        }

        TotpSecret? existing = await _repo.FindTotpSecretByAccountAsync(accountId, ct);
        if (existing is not null && existing.ConfirmedAt is not null)
        {
            throw new ConflictException(
                code: ErrorCodes.AuthTotpAlreadyEnrolled,
                message: "TOTP is already enrolled for this account.");
        }

        TotpEnrollment enrollment = _totp.GenerateEnrollment(account.Email ?? account.Id.ToString());

        // Re-enrollment replaces the existing unconfirmed secret in place
        // to respect the uq_totp_secrets_account unique constraint, and
        // deletes stale recovery codes so a discarded enrollment's codes
        // cannot be redeemed against the new secret.
        var now = DateTime.UtcNow;
        if (existing is not null)
        {
            existing.SecretEncrypted = enrollment.EncryptedSecret;
            existing.EnrolledAt = now;
            existing.ConfirmedAt = null;
            existing.RevokedAt = null;
            await _repo.UpdateTotpSecretAsync(existing, ct);
            await _repo.DeleteRecoveryCodesForAccountAsync(accountId, ct);
        }
        else
        {
            await _repo.SaveTotpSecretAsync(new TotpSecret
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                SecretEncrypted = enrollment.EncryptedSecret,
                EnrolledAt = now,
            }, ct);
        }

        await _repo.SaveRecoveryCodesAsync(
            enrollment.RecoveryCodes.Select(rc => new TotpRecoveryCode
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                CodeHash = rc.Hash,
                CreatedAt = now,
            }), ct);

        return Ok(new TotpEnrollResponseDto(
            OtpAuthUri: enrollment.OtpAuthUri,
            RecoveryCodes: enrollment.RecoveryCodes.Select(rc => rc.Plaintext).ToList()));
    }

    [HttpPost("totp/confirm")]
    [Authorize]
    public async Task<ActionResult<StepUpResponseDto>> ConfirmTotp(
        [FromBody] TotpConfirmRequestDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            throw ValidationFromModelState();
        }

        Guid accountId = ResolveAccountId();
        TotpSecret? secret = await _repo.FindTotpSecretByAccountAsync(accountId, ct);
        if (secret is null)
        {
            throw new ConflictException(
                code: ErrorCodes.AuthTotpNotEnrolled,
                message: "No TOTP enrollment to confirm.");
        }

        if (!_totp.VerifyCode(secret.SecretEncrypted, dto.Code))
        {
            throw new ValidationException(
                "Invalid TOTP code.",
                code: ErrorCodes.AuthTotpInvalidCode);
        }

        var now = DateTime.UtcNow;
        await _repo.ConfirmTotpSecretAsync(secret.Id, now, ct);

        // Confirmation doubles as the first successful step-up.
        WebSession? session = HttpContext.Items["InstitutionWebSession"] as WebSession;
        if (session is not null)
        {
            await _sessions.MarkStepUpVerifiedAsync(session.Id, ct);
        }

        return Ok(new StepUpResponseDto(
            VerifiedAt: now,
            WindowSeconds: _opts.StepUpWindowMinutes * 60));
    }

    [HttpPost("totp/verify")]
    [Authorize]
    public async Task<ActionResult<StepUpResponseDto>> VerifyTotp(
        [FromBody] TotpVerifyRequestDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            throw ValidationFromModelState();
        }

        Guid accountId = ResolveAccountId();
        TotpSecret? secret = await _repo.FindTotpSecretByAccountAsync(accountId, ct);
        if (secret is null || secret.ConfirmedAt is null)
        {
            throw new ConflictException(
                code: ErrorCodes.AuthTotpNotConfirmed,
                message: "TOTP is not enrolled or not confirmed.");
        }

        if (!_totp.VerifyCode(secret.SecretEncrypted, dto.Code))
        {
            throw new ValidationException(
                "Invalid TOTP code.",
                code: ErrorCodes.AuthTotpInvalidCode);
        }

        var now = DateTime.UtcNow;
        WebSession? session = HttpContext.Items["InstitutionWebSession"] as WebSession;
        if (session is not null)
        {
            await _sessions.MarkStepUpVerifiedAsync(session.Id, ct);
        }

        return Ok(new StepUpResponseDto(
            VerifiedAt: now,
            WindowSeconds: _opts.StepUpWindowMinutes * 60));
    }

    // ---- Session ------------------------------------------------------

    [HttpPost("session/refresh")]
    [Authorize]
    public ActionResult<SessionRefreshedResponseDto> RefreshSession()
    {
        WebSession? session = HttpContext.Items["InstitutionWebSession"] as WebSession;
        // Middleware already touched last_activity_at — the response
        // echoes the fresh timestamp so the client's soft-warning timer
        // can reset without a second round-trip.
        DateTime lastActivity = session?.LastActivityAt ?? DateTime.UtcNow;
        return Ok(new SessionRefreshedResponseDto(
            LastActivityAt: lastActivity,
            SoftWarningSeconds: _opts.SessionSoftWarningMinutes * 60,
            IdleTimeoutSeconds: _opts.SessionIdleMinutes * 60));
    }

    [HttpPost("session/step-up")]
    [Authorize]
    public async Task<ActionResult<StepUpResponseDto>> StepUp(
        [FromBody] StepUpRequestDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            throw ValidationFromModelState();
        }

        Guid accountId = ResolveAccountId();
        TotpSecret? secret = await _repo.FindTotpSecretByAccountAsync(accountId, ct);
        if (secret is null || secret.ConfirmedAt is null)
        {
            throw new ConflictException(
                code: ErrorCodes.AuthTotpNotConfirmed,
                message: "TOTP is not enrolled or not confirmed.");
        }

        if (!_totp.VerifyCode(secret.SecretEncrypted, dto.Code))
        {
            throw new ValidationException(
                "Invalid TOTP code.",
                code: ErrorCodes.AuthTotpInvalidCode);
        }

        var now = DateTime.UtcNow;
        WebSession? session = HttpContext.Items["InstitutionWebSession"] as WebSession;
        if (session is not null)
        {
            await _sessions.MarkStepUpVerifiedAsync(session.Id, ct);
        }

        return Ok(new StepUpResponseDto(
            VerifiedAt: now,
            WindowSeconds: _opts.StepUpWindowMinutes * 60));
    }

    [HttpPost("session/logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (HttpContext.Items["InstitutionWebSession"] is WebSession session)
        {
            await _sessions.RevokeAsync(session.Id, ct);
        }
        ClearSessionCookies();
        return NoContent();
    }

    // ------------------------------------------------------------------
    // Internal helpers
    // ------------------------------------------------------------------

    private Guid ResolveAccountId()
    {
        if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
        {
            return id;
        }
        throw new UnauthorizedException(ErrorCodes.AuthUnauthorized, "Authentication required.");
    }

    private void IssueSessionCookies(SessionCreated created)
    {
        var sessionExpires = created.Session.AbsoluteExpiresAt;
        var sessionOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = _opts.RequireSecureCookies,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = sessionExpires,
            MaxAge = sessionExpires - DateTime.UtcNow,
        };
        Response.Cookies.Append(_opts.SessionCookieName, created.SessionTokenPlaintext, sessionOptions);

        // CSRF cookie is intentionally NOT httpOnly so the web app can
        // read it and copy into the X-CSRF-Token header (double-submit
        // pattern). Same SameSite + Secure posture otherwise.
        var csrfOptions = new CookieOptions
        {
            HttpOnly = false,
            Secure = _opts.RequireSecureCookies,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = sessionExpires,
            MaxAge = sessionExpires - DateTime.UtcNow,
        };
        Response.Cookies.Append(_opts.CsrfCookieName, created.CsrfTokenPlaintext, csrfOptions);
    }

    private void ClearSessionCookies()
    {
        var expireOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = _opts.RequireSecureCookies,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = DateTimeOffset.UnixEpoch,
        };
        Response.Cookies.Delete(_opts.SessionCookieName, expireOptions);
        Response.Cookies.Delete(_opts.CsrfCookieName, new CookieOptions
        {
            HttpOnly = false,
            Secure = _opts.RequireSecureCookies,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = DateTimeOffset.UnixEpoch,
        });
    }

    private SessionEstablishedResponseDto BuildSessionBody(WebSession session) => new(
        CreatedAt: session.CreatedAt,
        AbsoluteExpiresAt: session.AbsoluteExpiresAt,
        SoftWarningSeconds: _opts.SessionSoftWarningMinutes * 60,
        IdleTimeoutSeconds: _opts.SessionIdleMinutes * 60);

    private ValidationException ValidationFromModelState()
    {
        var fieldErrors = new Dictionary<string, string[]>();
        foreach (var kv in ModelState)
        {
            if (kv.Value is not null && kv.Value.Errors.Count > 0)
            {
                fieldErrors[kv.Key] = kv.Value.Errors.Select(e => e.ErrorMessage).ToArray();
            }
        }
        return new ValidationException(
            "Request validation failed.",
            code: ErrorCodes.ValidationFailed,
            fieldErrors: fieldErrors);
    }
}
