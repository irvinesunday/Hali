using System;

namespace Hali.Domain.Entities.Auth;

/// <summary>
/// Email-delivered magic-link token used by institution-web to start a
/// session. Single-use + time-bounded — the token is hashed in storage so
/// the plaintext survives only in the issued URL and the row's
/// <see cref="ConsumedAt"/> stamp closes the replay window atomically.
/// </summary>
public class MagicLinkToken
{
    public Guid Id { get; set; }

    /// <summary>Email address the link was issued to (lowercased).</summary>
    public string DestinationEmail { get; set; } = string.Empty;

    /// <summary>SHA-256 hex of the token carried in the link URL.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// The account the link resolves to. Null when the email is not yet
    /// registered — the link flow itself creates the account on verify.
    /// </summary>
    public Guid? AccountId { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? ConsumedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
