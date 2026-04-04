using System;

namespace Hali.Domain.Entities.Auth;

public class Device
{
	public Guid Id { get; set; }

	public Guid? AccountId { get; set; }

	public string DeviceFingerprintHash { get; set; } = string.Empty;

	public string IntegrityLevel { get; set; } = "unknown";

	public string? Platform { get; set; }

	public string? AppVersion { get; set; }

	public string? ExpoPushToken { get; set; }

	public DateTime CreatedAt { get; set; }

	public DateTime LastSeenAt { get; set; }

	public bool IsBlocked { get; set; }
}
