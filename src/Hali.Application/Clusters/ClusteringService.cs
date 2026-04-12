using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Entities.Signals;
using Hali.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Hali.Application.Clusters;

public class ClusteringService : IClusteringService
{
    private readonly IClusterRepository _repo;

    private readonly IH3CellService _h3;

    private readonly ICivisEvaluationService _civis;

    private readonly CivisOptions _options;

    public ClusteringService(IClusterRepository repo, IH3CellService h3, ICivisEvaluationService civis, IOptions<CivisOptions> options)
    {
        _repo = repo;
        _h3 = h3;
        _civis = civis;
        _options = options.Value;
    }

    public async Task RouteSignalAsync(SignalEvent signal, CancellationToken ct = default(CancellationToken))
    {
        if (signal.SpatialCellId == null)
        {
            return;
        }
        string[] searchCells = _h3.GetKRingCells(signal.SpatialCellId, 1);
        IReadOnlyList<SignalCluster> candidates = await _repo.FindCandidateClustersAsync(searchCells, signal.Category, ct);
        SignalCluster bestCluster = null;
        double bestScore = 0.0;
        foreach (SignalCluster candidate in candidates)
        {
            double score = ComputeJoinScore(signal, candidate);
            if (score >= _options.JoinThreshold && score > bestScore)
            {
                bestScore = score;
                bestCluster = candidate;
            }
        }
        if (bestCluster != null)
        {
            await _repo.AttachToClusterAsync(bestCluster.Id, signal.Id, signal.DeviceId, "join", ct);
            bestCluster.LastSeenAt = DateTime.UtcNow;
            bestCluster.UpdatedAt = DateTime.UtcNow;
            bestCluster.RawConfirmationCount++;
            if (!string.IsNullOrEmpty(signal.ConditionSlug))
            {
                bestCluster.DominantConditionSlug = signal.ConditionSlug;
            }
            await _repo.UpdateClusterAsync(bestCluster, ct);
            await _repo.WriteOutboxEventAsync(new OutboxEvent
            {
                Id = Guid.NewGuid(),
                AggregateType = "cluster",
                AggregateId = bestCluster.Id,
                EventType = "cluster.updated",
                Payload = JsonSerializer.Serialize(new
                {
                    cluster_id = bestCluster.Id,
                    signal_event_id = signal.Id,
                    raw_confirmation_count = bestCluster.RawConfirmationCount
                }),
                OccurredAt = DateTime.UtcNow
            }, ct);
            await _civis.EvaluateClusterAsync(bestCluster.Id, ct);
        }
        else
        {
            DateTime now = DateTime.UtcNow;
            SignalCluster newCluster = new SignalCluster
            {
                Id = Guid.NewGuid(),
                Category = signal.Category,
                SubcategorySlug = signal.SubcategorySlug,
                DominantConditionSlug = signal.ConditionSlug,
                State = SignalState.Unconfirmed,
                SpatialCellId = signal.SpatialCellId,
                TemporalType = (signal.TemporalType ?? "episodic_unknown"),
                Title = BuildClusterTitle(signal),
                Summary = (signal.NeutralSummary ?? string.Empty),
                CreatedAt = now,
                UpdatedAt = now,
                FirstSeenAt = now,
                LastSeenAt = now,
                RawConfirmationCount = 1
            };
            await _repo.CreateClusterAsync(newCluster, signal.Id, signal.DeviceId, ct);
            await _repo.WriteOutboxEventAsync(new OutboxEvent
            {
                Id = Guid.NewGuid(),
                AggregateType = "cluster",
                AggregateId = newCluster.Id,
                EventType = "cluster.created",
                Payload = JsonSerializer.Serialize(new
                {
                    cluster_id = newCluster.Id,
                    signal_event_id = signal.Id,
                    category = signal.Category.ToString().ToLowerInvariant()
                }),
                OccurredAt = now
            }, ct);
            await _civis.EvaluateClusterAsync(newCluster.Id, ct);
        }
    }

    private double ComputeJoinScore(SignalEvent signal, SignalCluster cluster)
    {
        double num = ((signal.SpatialCellId == cluster.SpatialCellId) ? 1.0 : 0.75);
        double totalHours = (DateTime.UtcNow - cluster.LastSeenAt).TotalHours;
        double num2 = Math.Max(0.0, 1.0 - totalHours / _options.TimeScoreMaxAgeHours);
        double num3 = ((!string.IsNullOrEmpty(signal.ConditionSlug) && signal.ConditionSlug == cluster.DominantConditionSlug) ? 1.0 : 0.0);
        return 0.4 + 0.25 * num + 0.2 * num2 + 0.15 * num3;
    }

    private static string BuildClusterTitle(SignalEvent signal)
    {
        string text = signal.Category.ToString().ToLowerInvariant();
        string text2 = (string.IsNullOrEmpty(signal.SubcategorySlug) ? "" : (" — " + signal.SubcategorySlug));
        return text + text2;
    }
}
