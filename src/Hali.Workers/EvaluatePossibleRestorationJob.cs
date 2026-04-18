using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Application.Notifications;
using Hali.Application.Observability;
using Hali.Application.Participation;
using Hali.Domain.Entities.Clusters;
using Hali.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hali.Workers;

public sealed class EvaluatePossibleRestorationJob(
    IServiceScopeFactory scopeFactory,
    ILogger<EvaluatePossibleRestorationJob> logger) : BackgroundService()
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RunPassAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "EvaluatePossibleRestorationJob pass failed");
            }
        }
    }

    private async Task RunPassAsync(CancellationToken ct)
    {
        logger.LogInformation("{job} {event}", "EvaluatePossibleRestorationJob", "start");
        await using var scope = scopeFactory.CreateAsyncScope();
        var clusterRepo = scope.ServiceProvider.GetRequiredService<IClusterRepository>();
        var participationRepo = scope.ServiceProvider.GetRequiredService<IParticipationRepository>();
        var notificationQueue = scope.ServiceProvider.GetService<INotificationQueueService>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<CivisOptions>>().Value;

        var clusters = await clusterRepo.GetPossibleRestorationClustersAsync(ct);
        if (clusters.Count == 0)
        {
            logger.LogInformation("{job} {event} clustersProcessed=0", "EvaluatePossibleRestorationJob", "complete");
            return;
        }

        logger.LogInformation("EvaluatePossibleRestorationJob: evaluating {Count} cluster(s)", clusters.Count);

        int processed = 0;
        foreach (var cluster in clusters)
        {
            await EvaluateClusterAsync(cluster, clusterRepo, participationRepo, notificationQueue, options, ct);
            processed++;
        }
        logger.LogInformation("{job} {event} clustersProcessed={Count}", "EvaluatePossibleRestorationJob", "complete", processed);
    }

    private async Task EvaluateClusterAsync(
        SignalCluster cluster,
        IClusterRepository clusterRepo,
        IParticipationRepository participationRepo,
        INotificationQueueService? notificationQueue,
        CivisOptions options,
        CancellationToken ct)
    {
        // Atomic snapshot of restoration vote counts (#143). "Still affected"
        // votes are restoration_no rows: the HTTP write path maps the
        // "still_affected" response value to ParticipationType.RestorationNo
        // (see ParticipationService.RecordRestorationResponseAsync, fixed in
        // #142). The previous Affected-row count conflated initial "I'm
        // affected" reports with restoration-time dissents and is no longer
        // the correct signal for revert-to-active.
        RestorationCountSnapshot snapshot = await participationRepo.GetRestorationCountSnapshotAsync(cluster.Id, ct);
        int restorationYes = snapshot.YesVotes;
        int stillAffected = snapshot.NoVotes;
        int totalRestorationResponses = snapshot.TotalResponses;

        // Revert to active if still-affected votes overwhelm restoration votes.
        // Atomic: cluster state + CivisDecision + cluster.reverted_to_active outbox row
        // commit together. Previously the revert path wrote no outbox event at all
        // (#207 integration audit) — dashboards could never observe the reverse
        // transition. Emit it here so possible_restoration → active is visible.
        if (stillAffected > restorationYes && stillAffected >= options.MinRestorationAffectedVotes)
        {
            logger.LogInformation("{EventName} clusterId={ClusterId} stillAffected={StillAffected}",
                ObservabilityEvents.ClusterRevertedToActive, cluster.Id, stillAffected);
            DateTime now = DateTime.UtcNow;
            cluster.State = SignalState.Active;
            cluster.PossibleRestorationAt = null;
            cluster.UpdatedAt = now;
            CivisDecision revertDecision = new CivisDecision
            {
                Id = Guid.NewGuid(),
                ClusterId = cluster.Id,
                DecisionType = "revert_to_active",
                ReasonCodes = JsonSerializer.Serialize(new[] { "still_affected_votes_exceed_restoration" }),
                Metrics = JsonSerializer.Serialize(new { restoration_yes = restorationYes, still_affected = stillAffected }),
                CreatedAt = now
            };
            OutboxEvent revertEvent = new OutboxEvent
            {
                Id = Guid.NewGuid(),
                AggregateType = "signal_cluster",
                AggregateId = cluster.Id,
                EventType = ObservabilityEvents.ClusterRevertedToActive,
                SchemaVersion = ObservabilityEvents.SchemaVersionV1,
                Payload = JsonSerializer.Serialize(new
                {
                    cluster_id = cluster.Id,
                    from = ClustersMetrics.StatePossibleRestoration,
                    to = ClustersMetrics.StateActive,
                    trigger = "citizen_vote",
                    reason_code = "still_affected_votes_exceed_restoration",
                    restoration_yes = restorationYes,
                    still_affected = stillAffected
                }),
                OccurredAt = now
            };
            await clusterRepo.ApplyClusterTransitionAsync(cluster, revertDecision, revertEvent, ct);
            return;
        }

        // Resolve if restoration ratio and minimum votes met.
        // Atomic transition (#143): cluster + CivisDecision + outbox row commit together.
        if (totalRestorationResponses >= options.MinRestorationAffectedVotes)
        {
            double ratio = (double)restorationYes / (double)totalRestorationResponses;
            if (ratio >= options.RestorationRatio)
            {
                logger.LogInformation("{EventName} clusterId={ClusterId} ratio={Ratio}",
                    ObservabilityEvents.ClusterRestorationConfirmed, cluster.Id, ratio);
                DateTime now = DateTime.UtcNow;
                cluster.State = SignalState.Resolved;
                cluster.ResolvedAt = now;
                cluster.UpdatedAt = now;
                CivisDecision resolvedDecision = new CivisDecision
                {
                    Id = Guid.NewGuid(),
                    ClusterId = cluster.Id,
                    DecisionType = "resolved",
                    ReasonCodes = JsonSerializer.Serialize(new[] { "restoration_ratio_met" }),
                    Metrics = JsonSerializer.Serialize(new
                    {
                        restoration_yes = restorationYes,
                        total_restoration_responses = totalRestorationResponses,
                        ratio,
                        threshold = options.RestorationRatio
                    }),
                    CreatedAt = now
                };
                OutboxEvent resolvedEvent = new OutboxEvent
                {
                    Id = Guid.NewGuid(),
                    AggregateType = "signal_cluster",
                    AggregateId = cluster.Id,
                    EventType = ObservabilityEvents.ClusterRestorationConfirmed,
                    SchemaVersion = ObservabilityEvents.SchemaVersionV1,
                    Payload = JsonSerializer.Serialize(new
                    {
                        cluster_id = cluster.Id,
                        from = ClustersMetrics.StatePossibleRestoration,
                        to = ClustersMetrics.StateResolved,
                        trigger = "citizen_vote",
                        restoration_yes = restorationYes,
                        total_restoration_responses = totalRestorationResponses,
                        ratio,
                        threshold = options.RestorationRatio
                    }),
                    OccurredAt = now
                };
                await clusterRepo.ApplyClusterTransitionAsync(cluster, resolvedDecision, resolvedEvent, ct);

                if (notificationQueue != null)
                {
                    try
                    {
                        await notificationQueue.QueueClusterResolvedAsync(cluster.Id, cluster.LocalityId, cluster.Title ?? "Civic issue", ct);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to queue cluster_resolved notifications for {ClusterId}", cluster.Id);
                    }
                }
            }
        }
    }
}
