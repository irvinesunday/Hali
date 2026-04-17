using System;
using Hali.Domain.Enums;

namespace Hali.Domain.Entities.Auth;

public class OtpChallenge
{
    public Guid Id { get; set; }

    public Guid? AccountId { get; set; }

    public AuthMethod AuthMethod { get; set; }

    public string Destination { get; set; } = string.Empty;

    public string OtpHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? ConsumedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
