using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;

namespace Hali.Tests.Unit.Clusters;

internal sealed class FakeClusterRepo : IClusterRepository
{
	private readonly SignalCluster? _cluster;

	public int WrabCount { get; set; }

	public int ActiveMass { get; set; }

	public int UniqueDevices { get; set; }

	public List<CivisDecision> Decisions { get; } = new List<CivisDecision>();

	public List<OutboxEvent> OutboxEvents { get; } = new List<OutboxEvent>();

	public List<SignalCluster> Updates { get; } = new List<SignalCluster>();

	public FakeClusterRepo(SignalCluster? cluster = null)
	{
		_cluster = cluster;
	}

	public Task<SignalCluster?> GetClusterByIdAsync(Guid clusterId, CancellationToken ct)
	{
		return Task.FromResult(_cluster);
	}

	public Task<int> ComputeWrabCountAsync(Guid clusterId, int rollingWindowDays, CancellationToken ct)
	{
		return Task.FromResult(WrabCount);
	}

	public Task<int> ComputeActiveMassCountAsync(Guid clusterId, int horizonHours, CancellationToken ct)
	{
		return Task.FromResult(ActiveMass);
	}

	public Task<int> CountUniqueDevicesAsync(Guid clusterId, CancellationToken ct)
	{
		return Task.FromResult(UniqueDevices);
	}

	public Task UpdateClusterAsync(SignalCluster cluster, CancellationToken ct)
	{
		Updates.Add(cluster);
		return Task.CompletedTask;
	}

	public Task WriteCivisDecisionAsync(CivisDecision decision, CancellationToken ct)
	{
		Decisions.Add(decision);
		return Task.CompletedTask;
	}

	public Task WriteOutboxEventAsync(OutboxEvent outboxEvent, CancellationToken ct)
	{
		OutboxEvents.Add(outboxEvent);
		return Task.CompletedTask;
	}

	public Task<IReadOnlyList<SignalCluster>> FindCandidateClustersAsync(IEnumerable<string> spatialCells, CivicCategory category, CancellationToken ct)
	{
		return Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
	}

	public Task<SignalCluster> CreateClusterAsync(SignalCluster cluster, Guid signalEventId, Guid? deviceId, CancellationToken ct)
	{
		return Task.FromResult(cluster);
	}

	public Task AttachToClusterAsync(Guid clusterId, Guid signalEventId, Guid? deviceId, string linkReason, CancellationToken ct)
	{
		return Task.CompletedTask;
	}

	public Task<IReadOnlyList<SignalCluster>> GetActiveClustersForDecayAsync(CancellationToken ct)
	{
		return Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
	}

	public Task<IReadOnlyList<SignalCluster>> GetPossibleRestorationClustersAsync(CancellationToken ct)
	{
		return Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
	}

	public Task UpdateCountsAsync(Guid clusterId, int affectedCount, int observingCount, CancellationToken ct)
	{
		return Task.CompletedTask;
	}

	public Task<IReadOnlyList<SignalCluster>> GetActiveByLocalitiesAsync(IEnumerable<Guid> localityIds, CancellationToken ct)
	{
		return Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
	}

	public Task<IReadOnlyList<SignalCluster>> GetActiveByLocalitiesPagedAsync(IEnumerable<Guid> localityIds, bool? recurringOnly, int limit, DateTime? cursorBefore, CancellationToken ct)
	{
		return Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
	}

	public Task<IReadOnlyList<SignalCluster>> GetAllActivePagedAsync(IEnumerable<Guid> excludeLocalityIds, int limit, DateTime? cursorBefore, CancellationToken ct)
	{
		return Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
	}
}
