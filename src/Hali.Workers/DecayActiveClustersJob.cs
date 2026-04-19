using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Application.Observability;
using Hali.Domain.Entities.Clusters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hali.Workers;

public sealed class DecayActiveClustersJob(
    IServiceScopeFactory scopeFactory,
    ILogger<DecayActiveClustersJob> logger,
    WorkerMetrics metrics) : BackgroundService()
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5L);
    private const string JobType = WorkerMetrics.JobTypeDecayClusters;
    private const string Queue = "cluster_decay";
    private int _attempt;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            int attempt = ++_attempt;

            // Periodic worker — generate a new root correlation id per pass.
            var correlationId = Guid.NewGuid();

            logger.LogInformation(
                "{event_name} job_type={job_type} attempt={attempt} queue={queue} correlation_id={correlation_id} outcome={outcome}",
                ObservabilityEvents.WorkerJobStarted, JobType, attempt, Queue, correlationId, ObservabilityEvents.WorkerOutcome.Started);

            var sw = Stopwatch.StartNew();
            try
            {
                await RunDecayPassAsync(stoppingToken);
                sw.Stop();

                metrics.JobsProcessedTotal.Add(
                    1,
                    new KeyValuePair<string, object?>(WorkerMetrics.TagJobType, JobType),
                    new KeyValuePair<string, object?>(WorkerMetrics.TagOutcome, ObservabilityEvents.WorkerOutcome.Succeeded));

                metrics.JobDurationSeconds.Record(
                    sw.Elapsed.TotalSeconds,
                    new KeyValuePair<string, object?>(WorkerMetrics.TagJobType, JobType));

                logger.LogInformation(
                    "{event_name} job_type={job_type} attempt={attempt} queue={queue} correlation_id={correlation_id} outcome={outcome} duration_ms={duration_ms}",
                    ObservabilityEvents.WorkerJobSucceeded, JobType, attempt, Queue, correlationId, ObservabilityEvents.WorkerOutcome.Succeeded, sw.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                sw.Stop();
                metrics.JobsProcessedTotal.Add(
                    1,
                    new KeyValuePair<string, object?>(WorkerMetrics.TagJobType, JobType),
                    new KeyValuePair<string, object?>(WorkerMetrics.TagOutcome, ObservabilityEvents.WorkerOutcome.Cancelled));

                logger.LogInformation(
                    "{event_name} job_type={job_type} attempt={attempt} queue={queue} correlation_id={correlation_id} outcome={outcome}",
                    ObservabilityEvents.WorkerJobCancelled, JobType, attempt, Queue, correlationId, ObservabilityEvents.WorkerOutcome.Cancelled);
            }
            catch (Exception ex)
            {
                sw.Stop();
                metrics.JobsProcessedTotal.Add(
                    1,
                    new KeyValuePair<string, object?>(WorkerMetrics.TagJobType, JobType),
                    new KeyValuePair<string, object?>(WorkerMetrics.TagOutcome, ObservabilityEvents.WorkerOutcome.Failed));

                metrics.JobDurationSeconds.Record(
                    sw.Elapsed.TotalSeconds,
                    new KeyValuePair<string, object?>(WorkerMetrics.TagJobType, JobType));

                logger.LogError(
                    ex,
                    "{event_name} job_type={job_type} attempt={attempt} queue={queue} correlation_id={correlation_id} outcome={outcome}",
                    ObservabilityEvents.WorkerJobFailed, JobType, attempt, Queue, correlationId, ObservabilityEvents.WorkerOutcome.Failed);
            }
        }
    }

    private async Task RunDecayPassAsync(CancellationToken ct)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IClusterRepository clusterRepo = scope.ServiceProvider.GetRequiredService<IClusterRepository>();
        ICivisEvaluationService civis = scope.ServiceProvider.GetRequiredService<ICivisEvaluationService>();
        System.Collections.Generic.IReadOnlyList<SignalCluster> clusters = await clusterRepo.GetActiveClustersForDecayAsync(ct);
        if (clusters.Count == 0)
        {
            return;
        }
        logger.LogInformation("DecayActiveClustersJob: evaluating {Count} cluster(s)", clusters.Count);
        foreach (SignalCluster cluster in clusters)
        {
            await civis.ApplyDecayAsync(cluster.Id, ct);
        }
    }
}
