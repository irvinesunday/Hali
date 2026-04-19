using System;

namespace Hali.Domain.Entities.Marketing;

/// <summary>
/// Records a pre-launch early access signup from the marketing site.
/// Intentionally minimal — this is capture-edge data, not a core
/// domain entity. No FK to accounts: signups arrive before the
/// user has an account.
/// </summary>
public class EarlyAccessSignup
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public DateTime SubmittedAt { get; set; }
}
