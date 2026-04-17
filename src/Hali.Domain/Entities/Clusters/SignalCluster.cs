using System;
using Hali.Domain.Enums;

namespace Hali.Domain.Entities.Clusters;

public class SignalCluster
{
    public Guid Id { get; set; }

    public Guid? LocalityId { get; set; }

    public CivicCategory Category { get; set; }

    public string? SubcategorySlug { get; set; }

    public string? DominantConditionSlug { get; set; }

    public SignalState State { get; set; } = SignalState.Unconfirmed;

    public string? Title { get; set; }

    public string? Summary { get; set; }

    public Guid? LocationLabelId { get; set; }

    public string? SpatialCellId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime FirstSeenAt { get; set; }

    public DateTime LastSeenAt { get; set; }

    public DateTime? ActivatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public DateTime? PossibleRestorationAt { get; set; }

    public decimal? CivisScore { get; set; }

    public decimal? Wrab { get; set; }

    public decimal? Sds { get; set; }

    public int? Macf { get; set; }

    public int RawConfirmationCount { get; set; }

    public string? TemporalType { get; set; }

    public int AffectedCount { get; set; }

    public int ObservingCount { get; set; }

    /// <summary>
    /// Denormalized human-readable location label copied from the seed signal
    /// at cluster creation time. Used as the canonical location display text
    /// in feed cards and detail screens.
    /// </summary>
    public string? LocationLabelText { get; set; }
}
