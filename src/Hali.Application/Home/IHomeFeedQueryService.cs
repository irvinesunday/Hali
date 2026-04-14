using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hali.Contracts.Advisories;
using Hali.Domain.Entities.Clusters;

namespace Hali.Application.Home;

/// <summary>
/// Read-only query service for the home feed. Each method creates and
/// disposes its own isolated DbContext so callers may run multiple
/// methods concurrently via Task.WhenAll without DbContext thread-safety
/// violations.
/// </summary>
public interface IHomeFeedQueryService
{
    /// <summary>
    /// Returns active clusters for the specified localities with cursor-based pagination.
    /// When <paramref name="recurringOnly"/> is true, only recurring; false, only non-recurring; null, all.
    /// Caller should request limit+1 to detect whether a next page exists.
    /// </summary>
    Task<IReadOnlyList<SignalCluster>> GetActiveByLocalitiesPagedAsync(
        IEnumerable<Guid> localityIds, bool? recurringOnly, int limit,
        DateTime? cursorBefore, CancellationToken ct);

    /// <summary>
    /// Returns active clusters NOT in the specified localities with cursor-based pagination.
    /// Caller should request limit+1 to detect whether a next page exists.
    /// </summary>
    Task<IReadOnlyList<SignalCluster>> GetAllActivePagedAsync(
        IEnumerable<Guid> excludeLocalityIds, int limit,
        DateTime? cursorBefore, CancellationToken ct);

    /// <summary>
    /// Returns published official posts scoped to any of the specified localities,
    /// mapped to response DTOs. Results are de-duplicated by post id (a post
    /// scoped to multiple localities returns once) and ordered by CreatedAt
    /// descending. Single DI scope / DbContext — replaces the previous
    /// per-locality loop.
    /// </summary>
    Task<IReadOnlyList<OfficialPostResponseDto>> GetOfficialPostsByLocalitiesAsync(
        IEnumerable<Guid> localityIds, CancellationToken ct);
}
