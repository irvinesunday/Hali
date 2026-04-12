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
        int restorationYes = await participationRepo.CountByTypeAsync(cluster.Id, ParticipationType.RestorationYes, ct);
        int stillAffected = await participationRepo.CountByTypeAsync(cluster.Id, ParticipationType.Affected, ct);
        int totalRestorationResponses = await participationRepo.CountRestorationResponsesAsync(cluster.Id, ct);

        // Revert to active if still-affected votes overwhelm restoration votes
        if (stillAffected > restorationYes && stillAffected >= options.MinRestorationAffectedVotes)
        {
            logger.LogInformation("{EventName} clusterId={ClusterId} stillAffected={StillAffected}",
                ObservabilityEvents.ClusterRevertedToActive, cluster.Id, stillAffected);
            cluster.State = SignalState.Active;
            cluster.PossibleRestorationAt = null;
            cluster.UpdatedAt = DateTime.UtcNow;
            await clusterRepo.UpdateClusterAsync(cluster, ct);
            await clusterRepo.WriteCivisDecisionAsync(new CivisDecision
            {
                Id = Guid.NewGuid(),
                ClusterId = cluster.Id,
                DecisionType = "revert_to_active",
                ReasonCodes = "[\"still_affected_votes_exceed_restoration\"]",
                Metrics = JsonSerializer.Serialize(new { restoration_yes = restorationYes, still_affected = stillAffected }),
                CreatedAt = DateTime.UtcNow
            }, ct);
            return;
        }

        // Resolve if restoration ratio and minimum votes met
        if (totalRestorationResponses >= options.MinRestorationAffectedVotes)
        {
            double ratio = (double)restorationYes / (double)totalRestorationResponses;
            if (ratio >= options.RestorationRatio)
            {
                logger.LogInformation("{EventName} clusterId={ClusterId} ratio={Ratio}",
                    ObservabilityEvents.ClusterRestorationConfirmed, cluster.Id, ratio);
                cluster.State = SignalState.Resolved;
                cluster.ResolvedAt = DateTime.UtcNow;
                cluster.UpdatedAt = DateTime.UtcNow;
                await clusterRepo.UpdateClusterAsync(cluster, ct);
                await clusterRepo.WriteCivisDecisionAsync(new CivisDecision
                {
                    Id = Guid.NewGuid(),
                    ClusterId = cluster.Id,
                    DecisionType = "resolved",
                    ReasonCodes = "[\"restoration_ratio_met\"]",
                    Metrics = JsonSerializer.Serialize(new
                    {
                        restoration_yes = restorationYes,
                        total_restoration_responses = totalRestorationResponses,
                        ratio,
                        threshold = options.RestorationRatio
                    }),
                    CreatedAt = DateTime.UtcNow
                }, ct);
                await clusterRepo.WriteOutboxEventAsync(new OutboxEvent
                {
                    Id = Guid.NewGuid(),
                    AggregateType = "cluster",
                    AggregateId = cluster.Id,
                    EventType = "cluster_state_changed",
                    Payload = JsonSerializer.Serialize(new
                    {
                        cluster_id = cluster.Id,
                        from_state = "possible_restoration",
                        to_state = "resolved"
                    }),
                    OccurredAt = DateTime.UtcNow
                }, ct);

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
