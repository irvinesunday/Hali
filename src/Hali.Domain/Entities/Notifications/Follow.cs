using System;

namespace Hali.Domain.Entities.Notifications;

public class Follow
{
	public Guid Id { get; set; }

	public Guid AccountId { get; set; }

	public Guid LocalityId { get; set; }

	public DateTime CreatedAt { get; set; }
}
