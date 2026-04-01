using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;

namespace Hali.Application.Clusters;

public interface IClusterRepository
{
	Task<IReadOnlyList<SignalCluster>> FindCandidateClustersAsync(IEnumerable<string> spatialCells, CivicCategory category, CancellationToken ct);

	Task<SignalCluster?> GetClusterByIdAsync(Guid clusterId, CancellationToken ct);

	Task<SignalCluster> CreateClusterAsync(SignalCluster cluster, Guid signalEventId, Guid? deviceId, CancellationToken ct);

	Task AttachToClusterAsync(Guid clusterId, Guid signalEventId, Guid? deviceId, string linkReason, CancellationToken ct);

	Task UpdateClusterAsync(SignalCluster cluster, CancellationToken ct);

	Task<int> ComputeWrabCountAsync(Guid clusterId, int rollingWindowDays, CancellationToken ct);

	Task<int> ComputeActiveMassCountAsync(Guid clusterId, int horizonHours, CancellationToken ct);

	Task<int> CountUniqueDevicesAsync(Guid clusterId, CancellationToken ct);

	Task<IReadOnlyList<SignalCluster>> GetActiveClustersForDecayAsync(CancellationToken ct);

	Task<IReadOnlyList<SignalCluster>> GetPossibleRestorationClustersAsync(CancellationToken ct);

	Task WriteCivisDecisionAsync(CivisDecision decision, CancellationToken ct);

	Task WriteOutboxEventAsync(OutboxEvent outboxEvent, CancellationToken ct);

	Task UpdateCountsAsync(Guid clusterId, int affectedCount, int observingCount, CancellationToken ct);

	/// <summary>Returns active clusters for the specified localities.</summary>
	Task<IReadOnlyList<SignalCluster>> GetActiveByLocalitiesAsync(IEnumerable<Guid> localityIds, CancellationToken ct);
}
