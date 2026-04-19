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

public sealed class EvaluatePossibleRestorationJob(
    IServiceScopeFactory scopeFactory,
    ILogger<EvaluatePossibleRestorationJob> logger,
    WorkerMetrics metrics) : BackgroundService()
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private const string JobType = WorkerMetrics.JobTypeEvaluatePossibleRestoration;
    private const string Queue = "restoration_evaluation";
    private int _attempt;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            int attempt = ++_attempt;

            // Periodic worker — generate a new root correlation id per pass.
            var correlationId = Guid.NewGuid();

            logger.LogInformation(
                "{EventName} job_type={JobType} attempt={Attempt} queue={Queue} correlation_id={CorrelationId} outcome={Outcome}",
                ObservabilityEvents.WorkerJobStarted, JobType, attempt, Queue, correlationId, ObservabilityEvents.WorkerOutcome.Started);

            var sw = Stopwatch.StartNew();
            try
            {
                await RunPassAsync(stoppingToken);
                sw.Stop();

                metrics.JobsProcessedTotal.Add(
                    1,
                    new KeyValuePair<string, object?>(WorkerMetrics.TagJobType, JobType),
                    new KeyValuePair<string, object?>(WorkerMetrics.TagOutcome, ObservabilityEvents.WorkerOutcome.Succeeded));

                metrics.JobDurationSeconds.Record(
                    sw.Elapsed.TotalSeconds,
                    new KeyValuePair<string, object?>(WorkerMetrics.TagJobType, JobType));

                logger.LogInformation(
                    "{EventName} job_type={JobType} attempt={Attempt} queue={Queue} correlation_id={CorrelationId} outcome={Outcome} durationMs={DurationMs}",
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
                    "{EventName} job_type={JobType} attempt={Attempt} queue={Queue} correlation_id={CorrelationId} outcome={Outcome}",
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
                    "{EventName} job_type={JobType} attempt={Attempt} queue={Queue} correlation_id={CorrelationId} outcome={Outcome}",
                    ObservabilityEvents.WorkerJobFailed, JobType, attempt, Queue, correlationId, ObservabilityEvents.WorkerOutcome.Failed);
            }
        }
    }

    private async Task RunPassAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var clusterRepo = scope.ServiceProvider.GetRequiredService<IClusterRepository>();
        var restorationService = scope.ServiceProvider.GetRequiredService<IRestorationEvaluationService>();

        var clusters = await clusterRepo.GetPossibleRestorationClustersAsync(ct);
        if (clusters.Count == 0)
        {
            return;
        }

        logger.LogInformation("EvaluatePossibleRestorationJob: evaluating {Count} cluster(s)", clusters.Count);

        foreach (SignalCluster cluster in clusters)
        {
            await restorationService.EvaluateAsync(cluster, ct);
        }
    }
}
