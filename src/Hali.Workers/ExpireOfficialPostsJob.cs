using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Advisories;
using Hali.Application.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hali.Workers;

public sealed class ExpireOfficialPostsJob(
    IServiceScopeFactory scopeFactory,
    ILogger<ExpireOfficialPostsJob> logger,
    WorkerMetrics metrics) : BackgroundService()
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    private const string JobType = WorkerMetrics.JobTypeExpireOfficialPosts;
    private const string Queue = "official_posts_expiry";
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
                "{event_name} job_type={job_type} attempt={attempt} queue={queue} correlation_id={correlation_id} outcome={outcome}",
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

    private async Task RunPassAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<IOfficialPostRepository>();
        int expired = await repo.ExpirePostsAsync(ct);
        if (expired > 0)
            logger.LogInformation("ExpireOfficialPostsJob: expired {Count} post(s)", expired);
    }
}
