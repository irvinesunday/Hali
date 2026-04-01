using System.Text.Json;
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

    public ClusteringService(
        IClusterRepository repo,
        IH3CellService h3,
        ICivisEvaluationService civis,
        IOptions<CivisOptions> options)
    {
        _repo = repo;
        _h3 = h3;
        _civis = civis;
        _options = options.Value;
    }

    public async Task RouteSignalAsync(SignalEvent signal, CancellationToken ct = default)
    {
        if (signal.SpatialCellId is null)
            return; // cannot cluster without spatial info

        var searchCells = _h3.GetKRingCells(signal.SpatialCellId, 1);
        var candidates = await _repo.FindCandidateClustersAsync(searchCells, signal.Category, ct);

        SignalCluster? bestCluster = null;
        double bestScore = 0;

        foreach (var candidate in candidates)
        {
            var score = ComputeJoinScore(signal, candidate);
            if (score >= _options.JoinThreshold && score > bestScore)
            {
                bestScore = score;
                bestCluster = candidate;
            }
        }

        if (bestCluster is not null)
        {
            await _repo.AttachToClusterAsync(
                bestCluster.Id,
                signal.Id,
                signal.DeviceId,
                "join",
                ct);

            // Update cluster metadata
            bestCluster.LastSeenAt = DateTime.UtcNow;
            bestCluster.UpdatedAt = DateTime.UtcNow;
            bestCluster.RawConfirmationCount += 1;

            // Update dominant condition if this signal has one
            if (!string.IsNullOrEmpty(signal.ConditionSlug))
                bestCluster.DominantConditionSlug = signal.ConditionSlug;

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
            var now = DateTime.UtcNow;
            var newCluster = new SignalCluster
            {
                Id = Guid.NewGuid(),
                Category = signal.Category,
                SubcategorySlug = signal.SubcategorySlug,
                DominantConditionSlug = signal.ConditionSlug,
                State = SignalState.Unconfirmed,
                SpatialCellId = signal.SpatialCellId,
                TemporalType = signal.TemporalType ?? "episodic_unknown",
                Title = BuildClusterTitle(signal),
                Summary = signal.NeutralSummary ?? string.Empty,
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

    // join_score = 0.40 * category_match + 0.25 * distance_score + 0.20 * time_score + 0.15 * condition_match
    // category_match is always 1.0 — candidates are already filtered by category
    private double ComputeJoinScore(SignalEvent signal, SignalCluster cluster)
    {
        const double categoryMatch = 1.0;

        var distanceScore = signal.SpatialCellId == cluster.SpatialCellId ? 1.0 : 0.75;

        var ageHours = (DateTime.UtcNow - cluster.LastSeenAt).TotalHours;
        var timeScore = Math.Max(0.0, 1.0 - ageHours / _options.TimeScoreMaxAgeHours);

        var conditionMatch = !string.IsNullOrEmpty(signal.ConditionSlug)
            && signal.ConditionSlug == cluster.DominantConditionSlug
            ? 1.0 : 0.0;

        return 0.40 * categoryMatch
             + 0.25 * distanceScore
             + 0.20 * timeScore
             + 0.15 * conditionMatch;
    }

    private static string BuildClusterTitle(SignalEvent signal)
    {
        var category = signal.Category.ToString().ToLowerInvariant();
        var sub = string.IsNullOrEmpty(signal.SubcategorySlug) ? "" : $" — {signal.SubcategorySlug}";
        return $"{category}{sub}";
    }
}
