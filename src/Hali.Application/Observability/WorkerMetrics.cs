using System;
using System.Diagnostics.Metrics;

namespace Hali.Application.Observability;

/// <summary>
/// Hosts the <c>Hali.Workers</c> <see cref="Meter"/> and the instruments
/// covering background worker throughput and latency.
///
/// Every <see cref="Microsoft.Extensions.Hosting.BackgroundService"/> in
/// <c>Hali.Workers</c> records its job pickup, success, and failure events
/// via <see cref="JobsProcessedTotal"/> and <see cref="JobDurationSeconds"/>.
/// Tags are kept to a bounded set so the metric cardinality stays constant
/// regardless of load.
///
/// The meter is registered on the OpenTelemetry meter provider in
/// <c>WorkerProgram.cs</c> under the name <see cref="MeterName"/>. When
/// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is unset the meter and its instruments
/// still exist in-process (zero-cost, non-exported) and no behaviour regresses.
/// </summary>
public sealed class WorkerMetrics : IDisposable
{
    /// <summary>
    /// Name of the workers <see cref="Meter"/>.
    /// </summary>
    public const string MeterName = "Hali.Workers";

    /// <summary>Counter name — total jobs processed by the worker tier.</summary>
    public const string JobsProcessedTotalName = "worker_jobs_processed_total";

    /// <summary>Histogram name — job execution duration in seconds.</summary>
    public const string JobDurationSecondsName = "worker_job_duration_seconds";

    // ── Tag keys ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Tag key identifying which worker type processed the job.
    /// Values are a bounded set of worker names
    /// (e.g. <see cref="JobTypeOutboxRelay"/>, <see cref="JobTypeDecayClusters"/>).
    /// </summary>
    public const string TagJobType = "job_type";

    /// <summary>
    /// Tag key identifying the processing outcome.
    /// Values come from <see cref="ObservabilityEvents.WorkerOutcome"/>.
    /// </summary>
    public const string TagOutcome = "outcome";

    // ── Bounded job_type tag values ───────────────────────────────────────────

    /// <summary>Outbox relay worker — relays pending outbox events.</summary>
    public const string JobTypeOutboxRelay = "outbox_relay";

    /// <summary>Decay worker — applies CIVIS decay to active clusters.</summary>
    public const string JobTypeDecayClusters = "decay_clusters";

    /// <summary>Restoration evaluator — evaluates possible-restoration clusters.</summary>
    public const string JobTypeEvaluatePossibleRestoration = "evaluate_possible_restoration";

    /// <summary>Official posts expiry worker — expires time-bounded official posts.</summary>
    public const string JobTypeExpireOfficialPosts = "expire_official_posts";

    /// <summary>Push notification dispatch worker.</summary>
    public const string JobTypeSendPushNotifications = "send_push_notifications";

    private readonly Meter _meter;

    /// <summary>
    /// <c>worker_jobs_processed_total</c> — incremented once per pass of each
    /// background worker, tagged by job type and outcome.
    ///
    /// Tags (bounded — 5 × 3 = 15 combinations max):
    /// <list type="bullet">
    ///   <item><description><see cref="TagJobType"/> — bounded worker name constant.</description></item>
    ///   <item><description><see cref="TagOutcome"/> — one of
    ///     <c>succeeded | failed | cancelled</c>.</description></item>
    /// </list>
    /// No cluster id, event id, correlation id, attempt count, or any free-text
    /// is ever attached to this counter.
    /// </summary>
    public Counter<long> JobsProcessedTotal { get; }

    /// <summary>
    /// <c>worker_job_duration_seconds</c> — histogram of the wall-clock time
    /// spent inside each worker pass from pickup to completion (or failure),
    /// tagged by job type only.
    ///
    /// The <c>outcome</c> tag is intentionally omitted from the histogram
    /// because the counter already provides the per-outcome breakdown.
    /// Keeping the histogram single-dimensional keeps the number of produced
    /// time series bounded at 5 (one per worker type).
    /// </summary>
    public Histogram<double> JobDurationSeconds { get; }

    public WorkerMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        _meter = meterFactory.Create(MeterName);

        JobsProcessedTotal = _meter.CreateCounter<long>(
            name: JobsProcessedTotalName,
            unit: "{job}",
            description: "Total number of worker job passes completed, tagged by job_type and outcome.");

        JobDurationSeconds = _meter.CreateHistogram<double>(
            name: JobDurationSecondsName,
            unit: "s",
            description: "Duration of each worker job pass in seconds, tagged by job_type.");
    }

    public void Dispose() => _meter.Dispose();
}
