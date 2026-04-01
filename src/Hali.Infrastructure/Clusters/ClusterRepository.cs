using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;
using Hali.Infrastructure.Data.Clusters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Hali.Infrastructure.Clusters;

public class ClusterRepository : IClusterRepository
{
	private readonly ClustersDbContext _db;

	public ClusterRepository(ClustersDbContext db)
	{
		_db = db;
	}

	public async Task<IReadOnlyList<SignalCluster>> FindCandidateClustersAsync(IEnumerable<string> spatialCells, CivicCategory category, CancellationToken ct)
	{
		List<string> cells = spatialCells.ToList();
		return await _db.SignalClusters.Where((SignalCluster c) => cells.Contains(c.SpatialCellId) && (int)c.Category == (int)category && ((int)c.State == 0 || (int)c.State == 1)).ToListAsync(ct);
	}

	public async Task<SignalCluster?> GetClusterByIdAsync(Guid clusterId, CancellationToken ct)
	{
		return await _db.SignalClusters.FindAsync(new object[1] { clusterId }, ct);
	}

	public async Task<SignalCluster> CreateClusterAsync(SignalCluster cluster, Guid signalEventId, Guid? deviceId, CancellationToken ct)
	{
		await using (IDbContextTransaction tx = await _db.Database.BeginTransactionAsync(ct))
		{
			_db.SignalClusters.Add(cluster);
			await _db.SaveChangesAsync(ct);
			_db.ClusterEventLinks.Add(new ClusterEventLink
			{
				Id = Guid.NewGuid(),
				ClusterId = cluster.Id,
				SignalEventId = signalEventId,
				DeviceId = deviceId,
				LinkReason = "created",
				LinkedAt = DateTime.UtcNow
			});
			await _db.SaveChangesAsync(ct);
			await tx.CommitAsync(ct);
		}
		return cluster;
	}

	public async Task AttachToClusterAsync(Guid clusterId, Guid signalEventId, Guid? deviceId, string linkReason, CancellationToken ct)
	{
		_db.ClusterEventLinks.Add(new ClusterEventLink
		{
			Id = Guid.NewGuid(),
			ClusterId = clusterId,
			SignalEventId = signalEventId,
			DeviceId = deviceId,
			LinkReason = linkReason,
			LinkedAt = DateTime.UtcNow
		});
		await _db.SaveChangesAsync(ct);
	}

	public async Task UpdateClusterAsync(SignalCluster cluster, CancellationToken ct)
	{
		_db.SignalClusters.Update(cluster);
		await _db.SaveChangesAsync(ct);
	}

	public async Task<int> ComputeWrabCountAsync(Guid clusterId, int rollingWindowDays, CancellationToken ct)
	{
		DateTime cutoff = DateTime.UtcNow.AddDays(-rollingWindowDays);
		return await _db.ClusterEventLinks.Where((ClusterEventLink l) => l.ClusterId == clusterId && l.LinkedAt >= cutoff).CountAsync(ct);
	}

	public async Task<int> ComputeActiveMassCountAsync(Guid clusterId, int horizonHours, CancellationToken ct)
	{
		DateTime cutoff = DateTime.UtcNow.AddHours(-horizonHours);
		return await _db.ClusterEventLinks.Where((ClusterEventLink l) => l.ClusterId == clusterId && l.LinkedAt >= cutoff).CountAsync(ct);
	}

	public async Task<int> CountUniqueDevicesAsync(Guid clusterId, CancellationToken ct)
	{
		return await _db.Database.SqlQuery<int>($"SELECT COUNT(DISTINCT se.device_id)::int AS \"Value\"\n               FROM cluster_event_links cel\n               JOIN signal_events se ON se.id = cel.signal_event_id\n               WHERE cel.cluster_id = {clusterId} AND se.device_id IS NOT NULL").FirstOrDefaultAsync(ct);
	}

	public async Task<IReadOnlyList<SignalCluster>> GetActiveClustersForDecayAsync(CancellationToken ct)
	{
		return await _db.SignalClusters.Where((SignalCluster c) => (int)c.State == 1 || (int)c.State == 2).ToListAsync(ct);
	}

	public async Task<IReadOnlyList<SignalCluster>> GetPossibleRestorationClustersAsync(CancellationToken ct)
	{
		// SignalState.PossibleRestoration = 2
		return await _db.SignalClusters.Where((SignalCluster c) => (int)c.State == 2).ToListAsync(ct);
	}

	public async Task WriteCivisDecisionAsync(CivisDecision decision, CancellationToken ct)
	{
		_db.CivisDecisions.Add(decision);
		await _db.SaveChangesAsync(ct);
	}

	public async Task WriteOutboxEventAsync(OutboxEvent outboxEvent, CancellationToken ct)
	{
		_db.OutboxEvents.Add(outboxEvent);
		await _db.SaveChangesAsync(ct);
	}

	public async Task UpdateCountsAsync(Guid clusterId, int affectedCount, int observingCount, CancellationToken ct)
	{
		SignalCluster cluster = await _db.SignalClusters.FindAsync(new object[1] { clusterId }, ct);
		if (cluster != null)
		{
			cluster.AffectedCount = affectedCount;
			cluster.ObservingCount = observingCount;
			cluster.UpdatedAt = DateTime.UtcNow;
			await _db.SaveChangesAsync(ct);
		}
	}

	public async Task<IReadOnlyList<SignalCluster>> GetActiveByLocalitiesAsync(IEnumerable<Guid> localityIds, CancellationToken ct)
	{
		var ids = localityIds.ToList();
		return await _db.SignalClusters
			.Where(c => c.LocalityId != null && ids.Contains(c.LocalityId.Value) && (int)c.State == 1)
			.OrderByDescending(c => c.ActivatedAt)
			.ToListAsync(ct);
	}
}
