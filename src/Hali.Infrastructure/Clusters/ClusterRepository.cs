using Hali.Application.Clusters;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;
using Hali.Infrastructure.Data.Clusters;
using Microsoft.EntityFrameworkCore;

namespace Hali.Infrastructure.Clusters;

public class ClusterRepository : IClusterRepository
{
    private readonly ClustersDbContext _db;

    public ClusterRepository(ClustersDbContext db) => _db = db;

    public async Task<IReadOnlyList<SignalCluster>> FindCandidateClustersAsync(
        IEnumerable<string> spatialCells,
        CivicCategory category,
        CancellationToken ct)
    {
        var cells = spatialCells.ToList();
        return await _db.SignalClusters
            .Where(c => cells.Contains(c.SpatialCellId!)
                && c.Category == category
                && (c.State == SignalState.Unconfirmed || c.State == SignalState.Active))
            .ToListAsync(ct);
    }

    public async Task<SignalCluster?> GetClusterByIdAsync(Guid clusterId, CancellationToken ct)
        => await _db.SignalClusters.FindAsync([clusterId], ct);

    public async Task<SignalCluster> CreateClusterAsync(
        SignalCluster cluster,
        Guid signalEventId,
        Guid? deviceId,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

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
        return cluster;
    }

    public async Task AttachToClusterAsync(
        Guid clusterId,
        Guid signalEventId,
        Guid? deviceId,
        string linkReason,
        CancellationToken ct)
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
        var cutoff = DateTime.UtcNow.AddDays(-rollingWindowDays);
        return await _db.ClusterEventLinks
            .Where(l => l.ClusterId == clusterId && l.LinkedAt >= cutoff)
            .CountAsync(ct);
    }

    public async Task<int> ComputeActiveMassCountAsync(Guid clusterId, int horizonHours, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddHours(-horizonHours);
        return await _db.ClusterEventLinks
            .Where(l => l.ClusterId == clusterId && l.LinkedAt >= cutoff)
            .CountAsync(ct);
    }

    public async Task<int> CountUniqueDevicesAsync(Guid clusterId, CancellationToken ct)
    {
        // device_id is stored on signal_events, not cluster_event_links — use raw SQL to count distinct devices
        // SqlQuery<int> requires the scalar column to be named "Value"
        var result = await _db.Database.SqlQuery<int>(
            $@"SELECT COUNT(DISTINCT se.device_id)::int AS ""Value""
               FROM cluster_event_links cel
               JOIN signal_events se ON se.id = cel.signal_event_id
               WHERE cel.cluster_id = {clusterId} AND se.device_id IS NOT NULL")
            .FirstOrDefaultAsync(ct);
        return result;
    }

    public async Task<IReadOnlyList<SignalCluster>> GetActiveClustersForDecayAsync(CancellationToken ct)
        => await _db.SignalClusters
            .Where(c => c.State == SignalState.Active || c.State == SignalState.PossibleRestoration)
            .ToListAsync(ct);

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
        var cluster = await _db.SignalClusters.FindAsync([clusterId], ct);
        if (cluster is null) return;
        cluster.AffectedCount = affectedCount;
        cluster.ObservingCount = observingCount;
        cluster.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
