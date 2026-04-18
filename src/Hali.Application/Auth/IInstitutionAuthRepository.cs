using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hali.Domain.Entities.Auth;

namespace Hali.Application.Auth;

/// <summary>
/// Storage boundary for the Phase 2 institution auth + session stack
/// (#197). Covers the magic-link, TOTP, and web-session tables introduced
/// in the AddInstitutionAuthAndSessionTables migration.
/// </summary>
public interface IInstitutionAuthRepository
{
    // ---- Magic link ---------------------------------------------------

    Task SaveMagicLinkAsync(MagicLinkToken token, CancellationToken ct);

    /// <summary>
    /// Atomically consumes a magic link token if it exists, is unexpired,
    /// and has not been consumed yet. Returns the row on success, or
    /// null when no row satisfies all three conditions — used by the
    /// verify endpoint so replay attempts cannot race a concurrent
    /// successful consume.
    /// </summary>
    Task<MagicLinkToken?> ConsumeMagicLinkAsync(
        string tokenHash, DateTime now, CancellationToken ct);

    // ---- TOTP ---------------------------------------------------------

    Task<TotpSecret?> FindTotpSecretByAccountAsync(Guid accountId, CancellationToken ct);

    Task SaveTotpSecretAsync(TotpSecret secret, CancellationToken ct);

    /// <summary>
    /// Updates an existing (unconfirmed) TOTP secret row with a freshly
    /// generated secret. Used by the enroll endpoint so re-enrollment
    /// does not hit the <c>uq_totp_secrets_account</c> unique constraint.
    /// </summary>
    Task UpdateTotpSecretAsync(TotpSecret secret, CancellationToken ct);

    /// <summary>
    /// Deletes all recovery codes for an account. Called during re-enrollment
    /// so stale codes from a prior enrollment cannot be redeemed after the
    /// secret has been rotated.
    /// </summary>
    Task DeleteRecoveryCodesForAccountAsync(Guid accountId, CancellationToken ct);

    Task ConfirmTotpSecretAsync(Guid totpSecretId, DateTime confirmedAt, CancellationToken ct);

    Task SaveRecoveryCodesAsync(
        IEnumerable<TotpRecoveryCode> codes, CancellationToken ct);

    /// <summary>
    /// Atomically marks the recovery code for <paramref name="accountId"/>
    /// with the matching hash as used, returning true iff an unused row
    /// was found and updated.
    /// </summary>
    Task<bool> ConsumeRecoveryCodeAsync(
        Guid accountId, string codeHash, DateTime usedAt, CancellationToken ct);

    // ---- Web sessions -------------------------------------------------

    Task SaveWebSessionAsync(WebSession session, CancellationToken ct);

    Task<WebSession?> FindActiveWebSessionAsync(string sessionTokenHash, DateTime now, CancellationToken ct);

    Task TouchWebSessionAsync(Guid sessionId, DateTime lastActivityAt, CancellationToken ct);

    Task RevokeWebSessionAsync(Guid sessionId, DateTime revokedAt, CancellationToken ct);

    Task MarkStepUpVerifiedAsync(Guid sessionId, DateTime verifiedAt, CancellationToken ct);
}
