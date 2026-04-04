using System;
using Hali.Domain.Enums;

namespace Hali.Domain.Entities.Signals;

public class TaxonomyCondition
{
	public Guid Id { get; set; }

	public CivicCategory Category { get; set; }

	public string ConditionSlug { get; set; } = string.Empty;

	public string DisplayName { get; set; } = string.Empty;

	public int Ordinal { get; set; }

	public bool IsPositive { get; set; }
}
