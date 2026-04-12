using System;
using Hali.Domain.Enums;

namespace Hali.Domain.Entities.Auth;

public class Account
{
    public Guid Id { get; set; }

    public AccountType AccountType { get; set; } = AccountType.Citizen;

    public string? DisplayName { get; set; }

    public string? Email { get; set; }

    public string? PhoneE164 { get; set; }

    public bool IsPhoneVerified { get; set; }

    public bool IsEmailVerified { get; set; }

    public string Status { get; set; } = "active";

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>JSON object: { "cluster_activated": true, "restoration_prompt": true, "cluster_resolved": true }</summary>
    public string? NotificationSettings { get; set; }

    /// <summary>Set for institution accounts; null for citizen and admin accounts.</summary>
    public Guid? InstitutionId { get; set; }

    public bool IsBlocked { get; set; }
}
