using System;

namespace Hali.Domain.Entities.Auth;

/// <summary>
/// Per-account TOTP enrollment. The base-32 shared secret is encrypted at
/// rest via ASP.NET Data Protection — the column stores only the
/// ciphertext. One row per account; re-enrolling replaces the existing row.
/// </summary>
public class TotpSecret
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    /// <summary>Data-Protection-wrapped ciphertext of the base-32 secret.</summary>
    public string SecretEncrypted { get; set; } = string.Empty;

    public DateTime EnrolledAt { get; set; }

    /// <summary>
    /// Set when the user first verifies a TOTP code (completes enrollment).
    /// Rows with <see cref="ConfirmedAt"/> null are pending and do NOT satisfy
    /// a step-up-auth challenge.
    /// </summary>
    public DateTime? ConfirmedAt { get; set; }

    public DateTime? RevokedAt { get; set; }
}
