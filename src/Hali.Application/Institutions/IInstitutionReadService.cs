using System;
using System.Threading;
using System.Threading.Tasks;
using Hali.Contracts.Clusters;
using Hali.Contracts.Institutions;

namespace Hali.Application.Institutions;

/// <summary>
/// Read-model service backing the institution operational dashboard
/// endpoints (<c>GET /v1/institution/*</c>). Every method expects the
/// authenticated institution's id from the JWT claim — the controller
/// resolves it and passes it through. Each method filters results by
/// the caller's institution scope server-side; an institution can never
/// observe another institution's data via any code path here.
/// </summary>
public interface IInstitutionReadService
{
    Task<InstitutionOverviewResponseDto> GetOverviewAsync(
        Guid institutionId,
        Guid? areaId,
        CancellationToken ct);

    Task<InstitutionSignalsResponseDto> GetSignalsAsync(
        Guid institutionId,
        Guid? areaId,
        string? state,
        string? cursor,
        int limit,
        CancellationToken ct);

    /// <summary>
    /// Returns the full cluster detail for a cluster inside the caller's
    /// institution scope. Throws <see cref="Hali.Application.Errors.NotFoundException"/>
    /// when the cluster does not exist OR when it falls outside the
    /// caller's jurisdiction — the 404 shape is deliberate so institution
    /// A cannot probe for the existence of clusters owned by institution B.
    /// </summary>
    Task<ClusterResponseDto> GetSignalDetailAsync(
        Guid institutionId,
        Guid clusterId,
        CancellationToken ct);

    Task<InstitutionAreasResponseDto> GetAreasAsync(
        Guid institutionId,
        CancellationToken ct);

    Task<InstitutionActivityResponseDto> GetActivityAsync(
        Guid institutionId,
        Guid? areaId,
        string? cursor,
        int limit,
        CancellationToken ct);

    /// <summary>
    /// Returns every cluster currently in <c>possible_restoration</c>
    /// inside the caller's jurisdiction, enriched with live restoration
    /// vote counts. The dashboard uses this to drive the Restoration
    /// queue surface introduced in Phase 4 (#207). Rows sort by
    /// <c>possible_restoration_at</c> ascending so operators see the
    /// clusters that have sat longest at the top.
    /// </summary>
    Task<InstitutionRestorationQueueResponseDto> GetRestorationQueueAsync(
        Guid institutionId,
        Guid? areaId,
        CancellationToken ct);
}
