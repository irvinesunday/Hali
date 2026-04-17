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

    /// <summary>
    /// Optional response status for <c>live_update</c> posts. Canonical
    /// wire vocabulary: <c>acknowledged</c>, <c>teams_dispatched</c>,
    /// <c>teams_on_site</c>, <c>work_ongoing</c>, <c>restoration_in_progress</c>,
    /// <c>service_restored</c>. Rejected with validation.invalid_response_status
    /// when supplied on any non-live_update post or with a value outside the
    /// canonical set.
    /// </summary>
    public string? ResponseStatus { get; set; }

    /// <summary>
    /// Optional severity for <c>scheduled_disruption</c> posts. Canonical
    /// wire vocabulary: <c>minor</c>, <c>moderate</c>, <c>major</c>. Rejected
    /// with validation.invalid_severity when supplied on any non-scheduled
    /// post or with a value outside the canonical set.
    /// </summary>
    public string? Severity { get; set; }
}
