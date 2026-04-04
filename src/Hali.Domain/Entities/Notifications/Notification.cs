using System;

namespace Hali.Domain.Entities.Notifications;

public class Notification
{
	public Guid Id { get; set; }

	public Guid AccountId { get; set; }

	public string Channel { get; set; } = string.Empty;

	public string NotificationType { get; set; } = string.Empty;

	public string? Payload { get; set; }

	public DateTime SendAfter { get; set; }

	public DateTime? SentAt { get; set; }

	public string Status { get; set; } = "queued";

	public string? DedupeKey { get; set; }
}
