using System;

namespace Hali.Domain.Entities.Notifications;

public class Follow
{
	public Guid Id { get; set; }

	public Guid AccountId { get; set; }

	public Guid LocalityId { get; set; }

	/// <summary>
	/// User-chosen area/estate name captured at follow-time
	/// (e.g. "South B", "Nairobi West"). Nullable for follows
	/// created before this column existed — UI falls back to
	/// the canonical ward_name in that case.
	/// </summary>
	public string? DisplayLabel { get; set; }

	public DateTime CreatedAt { get; set; }
}
