using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Notifications;
using Hali.Application.Observability;
using Hali.Application.Participation;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hali.Application.Clusters;

/// <summary>
/// Authoritative lifecycle manager for clusters in the
/// <c>possible_restoration</c> state. Implements the revert-to-active and
/// resolve-by-citizen-vote transitions.
///
/// Both transitions route through
/// <see cref="IClusterRepository.ApplyClusterTransitionAsync"/> — the single
/// atomic write that commits the cluster state change, the CIVIS decision, and
/// the outbox event together.
/// </summary>
public class RestorationEvaluationService : IRestorationEvaluationService
{
    private readonly IClusterRepository _clusterRepo;
    private readonly IParticipationRepository _participationRepo;
    private readonly CivisOptions _options;
    private readonly INotificationQueueService? _notificationQueue;
    private readonly ClustersMetrics? _metrics;
    private readonly ILogger<RestorationEvaluationService>? _logger;

    public RestorationEvaluationService(
        IClusterRepository clusterRepo,
        IParticipationRepository participationRepo,
        IOptions<CivisOptions> options,
        INotificationQueueService? notificationQueue = null,
        ClustersMetrics? metrics = null,
        ILogger<RestorationEvaluationService>? logger = null)
    {
        _clusterRepo = clusterRepo;
        _participationRepo = participationRepo;
        _options = options.Value;
        _notificationQueue = notificationQueue;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task EvaluateAsync(SignalCluster cluster, CancellationToken ct = default)
    {
        if (cluster.State != SignalState.PossibleRestoration)
        {
            return;
        }

        Guid clusterId = cluster.Id;
        RestorationCountSnapshot snapshot = await _participationRepo.GetRestorationCountSnapshotAsync(clusterId, ct);
        int restorationYes = snapshot.YesVotes;
        int stillAffected = snapshot.NoVotes;
        int totalRestorationResponses = snapshot.TotalResponses;

        // Revert to active when still-affected votes dominate.
        if (stillAffected > restorationYes && stillAffected >= _options.MinRestorationAffectedVotes)
        {
            _logger?.LogInformation("{EventName} clusterId={ClusterId} stillAffected={StillAffected}",
                ObservabilityEvents.ClusterRevertedToActive, clusterId, stillAffected);

            DateTime now = DateTime.UtcNow;
            cluster.State = SignalState.Active;
            cluster.PossibleRestorationAt = null;
            cluster.UpdatedAt = now;

            CivisDecision revertDecision = new CivisDecision
            {
                Id = Guid.NewGuid(),
                ClusterId = clusterId,
                DecisionType = "revert_to_active",
                ReasonCodes = JsonSerializer.Serialize(new[] { "still_affected_votes_exceed_restoration" }),
                Metrics = JsonSerializer.Serialize(new
                {
                    restoration_yes = restorationYes,
                    still_affected = stillAffected
                }),
                CreatedAt = now
            };
            OutboxEvent revertEvent = new OutboxEvent
            {
                Id = Guid.NewGuid(),
                AggregateType = "signal_cluster",
                AggregateId = clusterId,
                EventType = ObservabilityEvents.ClusterRevertedToActive,
                SchemaVersion = ObservabilityEvents.SchemaVersionV1,
                Payload = JsonSerializer.Serialize(new
                {
                    cluster_id = clusterId,
                    from = ClustersMetrics.StatePossibleRestoration,
                    to = ClustersMetrics.StateActive,
                    trigger = "citizen_vote",
                    reason_code = "still_affected_votes_exceed_restoration",
                    restoration_yes = restorationYes,
                    still_affected = stillAffected
                }),
                OccurredAt = now,
                CorrelationId = Guid.NewGuid(),
                CausationId = null,
            };

            await _clusterRepo.ApplyClusterTransitionAsync(cluster, revertDecision, revertEvent, ct);

            _metrics?.ClusterLifecycleTransitionsTotal.Add(
                1,
                new KeyValuePair<string, object?>(ClustersMetrics.TagFromState, ClustersMetrics.StatePossibleRestoration),
                new KeyValuePair<string, object?>(ClustersMetrics.TagToState, ClustersMetrics.StateActive));

            return;
        }

        // Resolve when restoration ratio and minimum vote thresholds are met.
        if (totalRestorationResponses >= _options.MinRestorationAffectedVotes)
        {
            double ratio = (double)restorationYes / (double)totalRestorationResponses;
            if (ratio >= _options.RestorationRatio)
            {
                _logger?.LogInformation("{EventName} clusterId={ClusterId} ratio={Ratio}",
                    ObservabilityEvents.ClusterRestorationConfirmed, clusterId, ratio);

                DateTime now = DateTime.UtcNow;
                cluster.State = SignalState.Resolved;
                cluster.ResolvedAt = now;
                cluster.UpdatedAt = now;

                CivisDecision resolvedDecision = new CivisDecision
                {
                    Id = Guid.NewGuid(),
                    ClusterId = clusterId,
                    DecisionType = "resolved",
                    ReasonCodes = JsonSerializer.Serialize(new[] { "restoration_ratio_met" }),
                    Metrics = JsonSerializer.Serialize(new
                    {
                        restoration_yes = restorationYes,
                        total_restoration_responses = totalRestorationResponses,
                        ratio,
                        threshold = _options.RestorationRatio
                    }),
                    CreatedAt = now
                };
                OutboxEvent resolvedEvent = new OutboxEvent
                {
                    Id = Guid.NewGuid(),
                    AggregateType = "signal_cluster",
                    AggregateId = clusterId,
                    EventType = ObservabilityEvents.ClusterRestorationConfirmed,
                    SchemaVersion = ObservabilityEvents.SchemaVersionV1,
                    Payload = JsonSerializer.Serialize(new
                    {
                        cluster_id = clusterId,
                        from = ClustersMetrics.StatePossibleRestoration,
                        to = ClustersMetrics.StateResolved,
                        trigger = "citizen_vote",
                        restoration_yes = restorationYes,
                        total_restoration_responses = totalRestorationResponses,
                        ratio,
                        threshold = _options.RestorationRatio
                    }),
                    OccurredAt = now,
                    CorrelationId = Guid.NewGuid(),
                    CausationId = null,
                };

                await _clusterRepo.ApplyClusterTransitionAsync(cluster, resolvedDecision, resolvedEvent, ct);

                _metrics?.ClusterLifecycleTransitionsTotal.Add(
                    1,
                    new KeyValuePair<string, object?>(ClustersMetrics.TagFromState, ClustersMetrics.StatePossibleRestoration),
                    new KeyValuePair<string, object?>(ClustersMetrics.TagToState, ClustersMetrics.StateResolved));

                if (_notificationQueue is not null)
                {
                    try
                    {
                        await _notificationQueue.QueueClusterResolvedAsync(
                            clusterId, cluster.LocalityId, cluster.Title ?? "Civic issue", ct);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger?.LogError(ex,
                            "Failed to queue cluster_resolved notifications for {ClusterId}", clusterId);
                    }
                }
            }
        }
    }
}
