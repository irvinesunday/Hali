using System;
using Hali.Domain.Enums;

namespace Hali.Domain.Entities.Participation;

public class Participation
{
	public Guid Id { get; set; }

	public Guid ClusterId { get; set; }

	public Guid? AccountId { get; set; }

	public Guid? DeviceId { get; set; }

	public ParticipationType ParticipationType { get; set; }

	public string? ContextText { get; set; }

	public DateTime CreatedAt { get; set; }

	public string? IdempotencyKey { get; set; }
}
