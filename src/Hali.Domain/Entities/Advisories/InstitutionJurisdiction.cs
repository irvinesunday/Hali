using System;

namespace Hali.Domain.Entities.Advisories;

public class InstitutionJurisdiction
{
	public Guid Id { get; set; }

	public Guid InstitutionId { get; set; }

	public Guid? LocalityId { get; set; }

	public string? CorridorName { get; set; }

	public DateTime CreatedAt { get; set; }
}
