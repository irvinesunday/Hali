using System;

namespace Hali.Domain.Entities.Auth;

public class InstitutionInvite
{
    public Guid Id { get; set; }

    public Guid InstitutionId { get; set; }

    /// <summary>SHA-256 hex of the raw invite token.</summary>
    public string InviteTokenHash { get; set; } = string.Empty;

    public Guid InvitedByAccountId { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
