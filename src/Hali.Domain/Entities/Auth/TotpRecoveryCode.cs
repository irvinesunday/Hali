using System;

namespace Hali.Domain.Entities.Auth;

/// <summary>
/// A single-use recovery code for an account whose authenticator device is
/// lost. Stored as SHA-256 hash — the plaintext is shown to the user
/// exactly once at enrollment time. Consumption is atomic via
/// <see cref="UsedAt"/>; re-presenting a used code is rejected.
/// </summary>
public class TotpRecoveryCode
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    /// <summary>SHA-256 hex of the recovery code plaintext.</summary>
    public string CodeHash { get; set; } = string.Empty;

    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
