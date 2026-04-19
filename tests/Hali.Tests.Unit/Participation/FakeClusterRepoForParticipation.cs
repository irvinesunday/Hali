using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;

namespace Hali.Tests.Unit.Participation;

internal sealed class FakeClusterRepoForParticipation : IClusterRepository
{
	private readonly SignalCluster? _cluster;

	public List<(int Affected, int Observing)> CountUpdates { get; } = new List<(int, int)>();

	public List<SignalCluster> ClusterUpdates { get; } = new List<SignalCluster>();

	public List<CivisDecision> Decisions { get; } = new List<CivisDecision>();

	public List<OutboxEvent> OutboxEvents { get; } = new List<OutboxEvent>();

	public FakeClusterRepoForParticipation(SignalCluster? cluster = null)
	{
		_cluster = cluster;
	}

	public Task<SignalCluster?> GetClusterByIdAsync(Guid clusterId, CancellationToken ct)
	{
		return Task.FromResult(_cluster);
	}

	public Task UpdateCountsAsync(Guid clusterId, int affectedCount, int observingCount, CancellationToken ct)
	{
		CountUpdates.Add((affectedCount, observingCount));
		if (_cluster != null)
		{
			_cluster.AffectedCount = affectedCount;
			_cluster.ObservingCount = observingCount;
		}
		return Task.CompletedTask;
	}

	public Task UpdateClusterAsync(SignalCluster cluster, CancellationToken ct)
	{
		ClusterUpdates.Add(cluster);
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

	public Task ApplyClusterTransitionAsync(SignalCluster cluster, CivisDecision? decision, OutboxEvent outboxEvent, CancellationToken ct)
	{
		ClusterUpdates.Add(cluster);
		if (decision != null) Decisions.Add(decision);
		OutboxEvents.Add(outboxEvent);
		return Task.CompletedTask;
	}

	public Task<IReadOnlyList<SignalCluster>> FindCandidateClustersAsync(IEnumerable<string> s, CivicCategory c, CancellationToken ct)
	{
		return Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
	}

	public Task<SignalCluster> CreateClusterAsync(SignalCluster cluster, Guid seId, Guid? dId, CancellationToken ct)
	{
		return Task.FromResult(cluster);
	}

	public Task AttachToClusterAsync(Guid cId, Guid seId, Guid? dId, string r, CancellationToken ct)
	{
		return Task.CompletedTask;
	}

	public Task<int> ComputeWrabCountAsync(Guid cId, int d, CancellationToken ct)
	{
		return Task.FromResult(0);
	}

	public Task<int> ComputeActiveMassCountAsync(Guid cId, int h, CancellationToken ct)
	{
		return Task.FromResult(0);
	}

	public Task<int> CountUniqueDevicesAsync(Guid cId, CancellationToken ct)
	{
		return Task.FromResult(0);
	}

	public Task<double> GetMinLocationConfidenceAsync(Guid clusterId, CancellationToken ct)
	{
		return Task.FromResult(1.0);
	}

	public Task<IReadOnlyList<SignalCluster>> GetActiveClustersForDecayAsync(CancellationToken ct)
	{
		return Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
	}

	public Task<IReadOnlyList<SignalCluster>> GetPossibleRestorationClustersAsync(CancellationToken ct)
	{
		return Task.FromResult((IReadOnlyList<SignalCluster>)Array.Empty<SignalCluster>());
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

	public Task<IReadOnlyList<OutboxEvent>> GetUnpublishedOutboxEventsAsync(int limit, CancellationToken ct)
		=> Task.FromResult((IReadOnlyList<OutboxEvent>)Array.Empty<OutboxEvent>());

	public Task MarkOutboxEventsPublishedAsync(IEnumerable<Guid> ids, CancellationToken ct)
		=> Task.CompletedTask;
}
