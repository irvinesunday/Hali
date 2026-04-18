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

	/// <summary>
	/// Returns the minimum (worst-case) location_confidence across all signal_events
	/// linked to the cluster. Used by the MACF geo-uncertainty uplift. Returns 1.0
	/// (full confidence) if the cluster has no linked events with a confidence value.
	/// </summary>
	Task<double> GetMinLocationConfidenceAsync(Guid clusterId, CancellationToken ct);

	Task<IReadOnlyList<SignalCluster>> GetActiveClustersForDecayAsync(CancellationToken ct);

	Task<IReadOnlyList<SignalCluster>> GetPossibleRestorationClustersAsync(CancellationToken ct);

	Task WriteCivisDecisionAsync(CivisDecision decision, CancellationToken ct);

	Task WriteOutboxEventAsync(OutboxEvent outboxEvent, CancellationToken ct);

	/// <summary>
	/// Persists a cluster state change, the accompanying CIVIS decision,
	/// and the outbox event in a single database transaction. All three
	/// writes commit together or none of them do — the atomicity rule
	/// from Phase 4 (see <c>docs/arch/02_api_contracts.md</c> §Outbox
	/// event envelope). Idempotency is enforced by the caller's state
	/// guard (only transition clusters in the expected pre-state) plus
	/// the worker-level checkpoint keys; the transaction ensures a
	/// retry cannot observe a partially-applied transition.
	/// </summary>
	Task ApplyClusterTransitionAsync(
		SignalCluster cluster,
		CivisDecision? decision,
		OutboxEvent outboxEvent,
		CancellationToken ct);

	/// <summary>Returns unpublished outbox events (published_at IS NULL), up to <paramref name="limit"/>.</summary>
	Task<IReadOnlyList<OutboxEvent>> GetUnpublishedOutboxEventsAsync(int limit, CancellationToken ct);

	/// <summary>Stamps published_at on the given outbox event rows.</summary>
	Task MarkOutboxEventsPublishedAsync(IEnumerable<Guid> ids, CancellationToken ct);

	Task UpdateCountsAsync(Guid clusterId, int affectedCount, int observingCount, CancellationToken ct);

	/// <summary>Returns active clusters for the specified localities.</summary>
	Task<IReadOnlyList<SignalCluster>> GetActiveByLocalitiesAsync(IEnumerable<Guid> localityIds, CancellationToken ct);

	/// <summary>
	/// Returns active clusters for the specified localities with cursor-based pagination.
	/// Returns <paramref name="limit"/> items. If <paramref name="recurringOnly"/> is true, only recurring clusters
	/// are returned; if false, only non-recurring; if null, all.
	/// Caller should request limit+1 to detect whether a next page exists.
	/// </summary>
	Task<IReadOnlyList<SignalCluster>> GetActiveByLocalitiesPagedAsync(
		IEnumerable<Guid> localityIds, bool? recurringOnly, int limit, DateTime? cursorBefore, CancellationToken ct);

	/// <summary>
	/// Returns active clusters NOT in the specified localities with cursor-based pagination.
	/// Caller should request limit+1 to detect whether a next page exists.
	/// </summary>
	Task<IReadOnlyList<SignalCluster>> GetAllActivePagedAsync(
		IEnumerable<Guid> excludeLocalityIds, int limit, DateTime? cursorBefore, CancellationToken ct);
}
