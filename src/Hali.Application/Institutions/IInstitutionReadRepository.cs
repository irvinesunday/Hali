using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hali.Application.Institutions;

/// <summary>
/// Read-only repository for the institution operational dashboard.
/// Every method is scoped server-side by <c>institution_id</c> — callers
/// never supply a locality or area filter that could escape the
/// caller's jurisdiction. The repository projects to DTO-shaped value
/// types so the service layer can compose responses without knowing
/// about EF Core.
/// </summary>
public interface IInstitutionReadRepository
{
    /// <summary>
    /// Resolves the locality ids in the caller's institution scope. Only
    /// jurisdictions with a non-null <c>locality_id</c> contribute; corridor-
    /// only jurisdictions report a zero-length scope for list queries until
    /// corridor→locality mapping is formalised. Callers should treat an
    /// empty result set as "no list queries can return anything" — not an
    /// error.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetScopeLocalityIdsAsync(
        Guid institutionId,
        Guid? areaId,
        CancellationToken ct);

    /// <summary>
    /// Lists every jurisdiction row (an "area") owned by the institution,
    /// enriched with display-ready fields derived from the referenced
    /// locality. Corridor-only jurisdictions use the corridor name as
    /// the display name.
    /// </summary>
    Task<IReadOnlyList<InstitutionAreaRow>> GetAreasAsync(
        Guid institutionId,
        CancellationToken ct);

    /// <summary>
    /// Lists signal clusters in the caller's institution scope with the
    /// fields the Live Signals page needs, sorted newest-first for a
    /// stable paging surface. <paramref name="localityIds"/> is the
    /// pre-computed set of localities in the caller's scope — passed
    /// explicitly so the service layer owns the scope derivation.
    /// </summary>
    Task<IReadOnlyList<InstitutionClusterRow>> ListClustersAsync(
        IReadOnlyList<Guid> localityIds,
        Guid? areaLocalityId,
        string? filterState,
        DateTime? cursorLastSeenAt,
        Guid? cursorId,
        int limit,
        CancellationToken ct);

    /// <summary>
    /// Counts open signal clusters in the institution's scope, bucketed
    /// for the Overview summary tiles.
    /// </summary>
    Task<InstitutionSignalCounts> GetSignalCountsAsync(
        IReadOnlyList<Guid> localityIds,
        DateTime asOf,
        CancellationToken ct);

    /// <summary>
    /// Counts recent reports (signal events) on a cluster within the
    /// configured 24-hour window — used to populate
    /// <c>recentReports24h</c> on signal list items.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> GetRecentReportCountsAsync(
        IReadOnlyList<Guid> clusterIds,
        DateTime since,
        CancellationToken ct);

    /// <summary>
    /// Resolves the most recent <c>live_update</c> response status per
    /// cluster. Returned map only contains clusters that have at least
    /// one live_update with a non-null response_status value.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> GetLatestResponseStatusesAsync(
        IReadOnlyList<Guid> clusterIds,
        CancellationToken ct);

    /// <summary>
    /// Returns the most recent live_update response status for a single
    /// cluster, or null if none has been posted.
    /// </summary>
    Task<string?> GetLatestResponseStatusForClusterAsync(
        Guid clusterId,
        CancellationToken ct);

    /// <summary>
    /// Lists activity feed items — state-change observations and newly
    /// posted official updates — scoped to the institution's localities.
    /// Sorted newest-first for cursor pagination.
    /// </summary>
    Task<IReadOnlyList<InstitutionActivityRow>> GetActivityAsync(
        IReadOnlyList<Guid> localityIds,
        Guid? areaLocalityId,
        DateTime? cursorTimestamp,
        Guid? cursorId,
        int limit,
        CancellationToken ct);

    /// <summary>
    /// Returns true if the given cluster's locality is inside the
    /// caller's institution scope. Used to gate the institution signal
    /// detail route so cross-institution lookups cannot leak.
    /// </summary>
    Task<bool> IsClusterInScopeAsync(
        Guid clusterId,
        IReadOnlyList<Guid> localityIds,
        CancellationToken ct);
}

/// <summary>Row-shaped projection of an institution jurisdiction.</summary>
public sealed record InstitutionAreaRow(
    Guid Id,
    Guid? LocalityId,
    string? CorridorName,
    string DisplayName,
    int ActiveSignals,
    string? TopCategory,
    DateTime? LastUpdatedAt);

/// <summary>Row-shaped projection of a signal cluster in institution scope.</summary>
public sealed record InstitutionClusterRow(
    Guid Id,
    string? Title,
    Guid? LocalityId,
    string? LocalityDisplayName,
    string Category,
    string State,
    int AffectedCount,
    int ObservingCount,
    DateTime CreatedAt,
    DateTime LastSeenAt,
    DateTime? ActivatedAt);

/// <summary>Bucketed signal counts for the institution Overview.</summary>
public sealed record InstitutionSignalCounts(
    int ActiveSignals,
    int GrowingSignals,
    int UpdatesPostedToday,
    int StabilisedToday);

/// <summary>Row-shaped projection of an activity feed item.</summary>
public sealed record InstitutionActivityRow(
    Guid Id,
    string Type,
    string Message,
    DateTime Timestamp,
    Guid? SignalId);
