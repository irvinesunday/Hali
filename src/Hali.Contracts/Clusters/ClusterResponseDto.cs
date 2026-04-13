using System;
using System.Collections.Generic;
using Hali.Contracts.Advisories;

namespace Hali.Contracts.Clusters;

public record ClusterResponseDto(
    Guid Id,
    string State,
    string Category,
    string? SubcategorySlug,
    string? Title,
    string? Summary,
    int AffectedCount,
    int ObservingCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ActivatedAt,
    DateTime? PossibleRestorationAt,
    DateTime? ResolvedAt)
{
    /// <summary>
    /// Canonical human-readable location label for this cluster, e.g.
    /// "Ngong Road near Adams Arcade, Kilimani". Null when the originating
    /// signal did not include a resolved location label.
    /// </summary>
    public string? LocationLabel { get; init; }

    public List<OfficialPostResponseDto> OfficialPosts { get; init; } = new();

    /// <summary>
    /// Per-caller participation summary used by the mobile UI to gate the
    /// "Add Further Context" and restoration-response CTAs server-side.
    /// Null for unauthenticated callers or when the caller has no
    /// participation record on this cluster.
    /// </summary>
    public MyParticipationDto? MyParticipation { get; init; }
}

/// <summary>
/// Snapshot of the authenticated caller's most recent participation on a
/// cluster, plus the server's verdict on whether the two restricted CTAs
/// may be shown. This is the source of truth — the mobile app must NOT
/// gate these CTAs on local state alone.
/// </summary>
public record MyParticipationDto(
    string Type,
    DateTime CreatedAt,
    bool CanAddContext,
    bool CanRespondToRestoration);
