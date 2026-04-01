using System;

namespace Hali.Domain.Entities.Advisories;

public class OfficialPostScope
{
	public Guid Id { get; set; }

	public Guid OfficialPostId { get; set; }

	public Guid? LocalityId { get; set; }

	public string? CorridorName { get; set; }
}
