using System;

namespace Hali.Contracts.Advisories;

public class CreateOfficialPostRequestDto
{
    public string Type { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public Guid? RelatedClusterId { get; set; }
    public bool IsRestorationClaim { get; set; }
    public Guid? LocalityId { get; set; }
    public string? CorridorName { get; set; }
}
