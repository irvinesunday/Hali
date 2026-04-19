using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Hali.Application.Clusters;
using Hali.Application.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Hali.Workers;

public sealed class OutboxRelayJob(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxRelayJob> logger,
    WorkerMetrics metrics) : BackgroundService()
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
    private const string JobType = WorkerMetrics.JobTypeOutboxRelay;
    private const string Queue = "outbox";
    private int _attempt;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            int attempt = ++_attempt;

            // Periodic worker — no parent outbox event. Generate a new root
            // correlation id per pass so each pass is independently traceable.
            var correlationId = Guid.NewGuid();

            logger.LogInformation(
                "{EventName} job_type={JobType} attempt={Attempt} queue={Queue} correlation_id={CorrelationId} outcome={Outcome}",
                ObservabilityEvents.WorkerJobStarted, JobType, attempt, Queue, correlationId, ObservabilityEvents.WorkerOutcome.Started);

            var sw = Stopwatch.StartNew();
            try
            {
                await RunRelayPassAsync(stoppingToken);
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

    private async Task RunRelayPassAsync(CancellationToken ct)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        var relay = scope.ServiceProvider.GetRequiredService<IOutboxRelayService>();
        var count = await relay.ProcessPendingAsync(ct);
        if (count > 0)
            logger.LogDebug("OutboxRelayJob: processed {Count} event(s)", count);
    }
}
