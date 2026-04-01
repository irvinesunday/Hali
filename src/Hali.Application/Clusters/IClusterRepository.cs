using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;

namespace Hali.Application.Clusters;

public interface IClusterRepository
{
    Task<IReadOnlyList<SignalCluster>> FindCandidateClustersAsync(
        IEnumerable<string> spatialCells,
        CivicCategory category,
        CancellationToken ct);

    Task<SignalCluster?> GetClusterByIdAsync(Guid clusterId, CancellationToken ct);

    Task<SignalCluster> CreateClusterAsync(SignalCluster cluster, Guid signalEventId, Guid? deviceId, CancellationToken ct);

    Task AttachToClusterAsync(Guid clusterId, Guid signalEventId, Guid? deviceId, string linkReason, CancellationToken ct);

    Task UpdateClusterAsync(SignalCluster cluster, CancellationToken ct);

    /// <summary>
    /// Count of signal event links within the WRAB rolling window.
    /// Each link counts as weight 1.0 for MVP.
    /// </summary>
    Task<int> ComputeWrabCountAsync(Guid clusterId, int rollingWindowDays, CancellationToken ct);

    /// <summary>
    /// Count of signal event links within the active mass evaluation horizon.
    /// </summary>
    Task<int> ComputeActiveMassCountAsync(Guid clusterId, int horizonHours, CancellationToken ct);

    /// <summary>
    /// Count of distinct device IDs on cluster_event_links for this cluster.
    /// NULL device_id counts as a single anonymous device per cluster.
    /// </summary>
    Task<int> CountUniqueDevicesAsync(Guid clusterId, CancellationToken ct);

    Task<IReadOnlyList<SignalCluster>> GetActiveClustersForDecayAsync(CancellationToken ct);

    Task WriteCivisDecisionAsync(CivisDecision decision, CancellationToken ct);

    Task WriteOutboxEventAsync(OutboxEvent outboxEvent, CancellationToken ct);

    Task UpdateCountsAsync(Guid clusterId, int affectedCount, int observingCount, CancellationToken ct);
}
