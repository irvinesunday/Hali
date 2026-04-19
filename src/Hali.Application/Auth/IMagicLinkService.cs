using System;
using System.Threading;
using System.Threading.Tasks;
using Hali.Domain.Entities.Auth;

namespace Hali.Application.Auth;

/// <summary>
/// Email magic-link flow used by institution-web for the first-factor
/// challenge. Tokens are 256-bit cryptographically random values carried
/// in the verification URL; the SHA-256 hash is what the database stores.
/// </summary>
public interface IMagicLinkService
{
    /// <summary>
    /// Issues a magic link for the given email. Returns the plaintext
    /// token + the URL a client should render — the URL itself is never
    /// logged. When the email has no registered account, the row is
    /// still created (with AccountId = null) — the verify flow will
    /// either reject (institution users must pre-exist) or create an
    /// account depending on policy. Rate-limited: max 3 requests per
    /// email per 15 minutes; throws <see cref="Hali.Application.Errors.RateLimitException"/>
    /// on excess. The calling IP is stored for audit purposes only.
    /// </summary>
    Task<MagicLinkIssued> IssueAsync(string email, string? ipAddress, CancellationToken ct);

    /// <summary>
    /// Atomically consumes a plaintext token and returns the matched row
    /// when the token exists, is unexpired, and has not been consumed
    /// yet. Null on any rejection — callers surface a single opaque
    /// error to avoid token-validity oracles.
    /// </summary>
    Task<MagicLinkToken?> ConsumeAsync(string plaintextToken, CancellationToken ct);

    /// <summary>SHA-256 hex hash of the token — same codec used in storage.</summary>
    string HashToken(string plaintextToken);
}

public sealed record MagicLinkIssued(
    string PlaintextToken,
    string Url,
    DateTime ExpiresAt);
