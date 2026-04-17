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

    /// <summary>
    /// Optional response status attached to a <c>live_update</c> post.
    /// The canonical wire vocabulary is <c>acknowledged</c>,
    /// <c>teams_dispatched</c>, <c>teams_on_site</c>, <c>work_ongoing</c>,
    /// <c>restoration_in_progress</c>, <c>service_restored</c> — validated at
    /// the application boundary by
    /// <c>Hali.Application.Institutions.InstitutionVocabulary.ResponseStatuses</c>.
    /// Null for post types other than <c>live_update</c> and for older rows
    /// written before this column existed.
    /// </summary>
    public string? ResponseStatus { get; set; }

    /// <summary>
    /// Optional severity attached to a <c>scheduled_disruption</c> post
    /// (<c>minor</c>, <c>moderate</c>, <c>major</c>). Null for post types other
    /// than <c>scheduled_disruption</c> and for older rows written before this
    /// column existed.
    /// </summary>
    public string? Severity { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
