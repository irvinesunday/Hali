using System;
using Hali.Domain.Enums;

namespace Hali.Domain.Entities.Advisories;

public class OfficialPost
{
	public Guid Id { get; set; }

	public Guid InstitutionId { get; set; }

	public Guid? AuthorAccountId { get; set; }

	public OfficialPostType Type { get; set; }

	public CivicCategory Category { get; set; }

	public string Title { get; set; } = string.Empty;

	public string Body { get; set; } = string.Empty;

	public DateTime? StartsAt { get; set; }

	public DateTime? EndsAt { get; set; }

	public string Status { get; set; } = "draft";

	public Guid? RelatedClusterId { get; set; }

	public bool IsRestorationClaim { get; set; }

	public DateTime CreatedAt { get; set; }

	public DateTime UpdatedAt { get; set; }
}
