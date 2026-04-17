using System;
using System.Threading;
using System.Threading.Tasks;
using Hali.Domain.Entities.Auth;

namespace Hali.Application.Auth;

/// <summary>
/// Web session lifecycle for institution + institution-admin users.
/// Server owns the timeout model end-to-end — the client never bypasses
/// a stale session by presenting a not-yet-expired token.
/// </summary>
public interface IInstitutionSessionService
{
    /// <summary>
    /// Mints a new session for the given account, returning the opaque
    /// session + CSRF tokens (plaintext, single use). The absolute
    /// expiry is stamped at creation and never extended.
    /// </summary>
    Task<SessionCreated> CreateAsync(Guid accountId, Guid? institutionId, CancellationToken ct);

    /// <summary>
    /// Resolves + validates a session cookie. Returns the session on
    /// success, or an enum value indicating WHY the session is rejected
    /// when not. Idle + absolute timeouts are enforced server-side here.
    /// </summary>
    Task<SessionValidation> ValidateAsync(string sessionTokenPlaintext, CancellationToken ct);

    /// <summary>
    /// Resets the idle timer on a session. Called by the keep-alive
    /// endpoint and implicitly by the middleware on every successful
    /// request.
    /// </summary>
    Task TouchAsync(Guid sessionId, CancellationToken ct);

    /// <summary>Revokes a session (logout / explicit termination).</summary>
    Task RevokeAsync(Guid sessionId, CancellationToken ct);

    /// <summary>Stamps a fresh step-up TOTP verify on the session.</summary>
    Task MarkStepUpVerifiedAsync(Guid sessionId, CancellationToken ct);

    /// <summary>
    /// Hashes a session-token plaintext to its stored column form. Exposed
    /// so middleware can perform lookups without going through the
    /// service's validation pipeline.
    /// </summary>
    string HashSessionToken(string plaintext);

    /// <summary>Hashes a CSRF-token plaintext for comparison against storage.</summary>
    string HashCsrfToken(string plaintext);
}

public sealed record SessionCreated(
    WebSession Session,
    string SessionTokenPlaintext,
    string CsrfTokenPlaintext);

public enum SessionValidationResult
{
    /// <summary>Session is valid — returned row is populated.</summary>
    Ok,
    /// <summary>Token missing, unknown, or revoked.</summary>
    Invalid,
    /// <summary>Session idle time exceeded the configured threshold.</summary>
    IdleTimeout,
    /// <summary>Session exceeded the 12-hour absolute limit.</summary>
    AbsoluteTimeout,
}

public sealed record SessionValidation(
    SessionValidationResult Result,
    WebSession? Session);
