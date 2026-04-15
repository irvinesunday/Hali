using System;
using System.Diagnostics.Metrics;

namespace Hali.Application.Observability;

/// <summary>
/// Hosts the <c>Hali.Signals</c> <see cref="Meter"/> and the instruments emitted
/// across the signal intake pipeline (R3.b, issue #167). The pipeline —
/// <c>SignalsController</c> → <c>SignalIngestionService</c> →
/// <c>AnthropicNlpExtractionService</c> → <c>ClusteringService</c> →
/// <c>CivisEvaluationService</c> — is the hottest write path in Phase 1. Before
/// this meter it had structured logs but no queryable time series for the three
/// operational questions the pilot must answer at a glance:
/// <list type="bullet">
///   <item><description><c>signals_preview_requests_total</c> /
///     <c>signals_submit_requests_total</c> — ingestion throughput counters
///     (tagged by request outcome only).</description></item>
///   <item><description><c>nlp_extraction_duration_seconds</c> — latency
///     histogram around the Anthropic HTTP call + parse span only, tagged by
///     NLP outcome so regressions on either the happy path or the fallback
///     path stay visible.</description></item>
///   <item><description><c>signal_join_outcome_total</c> — clustering decision
///     counter covering join-vs-create-new plus the downstream activation
///     transition that turns an unconfirmed cluster active.</description></item>
/// </list>
///
/// The meter is registered on the OpenTelemetry meter provider in
/// <c>Program.cs</c> under the name <see cref="MeterName"/>. When
/// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is unset the meter and its instruments
/// still exist in-process (zero-cost, non-exported) and no behaviour regresses.
///
/// The class lives in <c>Hali.Application.Observability</c> (rather than
/// <c>Hali.Api.Observability</c> where <c>HomeMetrics</c> lives) because the
/// instruments are emitted from three layers — API (controller counters),
/// Application (clustering + activation counter), and Infrastructure (NLP
/// histogram). The application project is the lowest common reference point
/// the other two already depend on.
///
/// Tag values are bounded by static catalogs — no request-derived string
/// (free_text, locality name, category, subcategory, idempotency key,
/// account id, device id, cluster id, spatial cell id, or NLP internals) is
/// ever attached. At steady state this meter produces 4 × 3 = 12 time series
/// at most, matching the cardinality envelope the observability backend can
/// budget for pilot.
/// </summary>
public sealed class SignalsMetrics : IDisposable
{
    /// <summary>
    /// Name of the signals <see cref="Meter"/>. Mirrored by
    /// <c>AddMeter(SignalsMetrics.MeterName)</c> on the OpenTelemetry meter
    /// provider so instruments export through the existing OTLP transport.
    /// </summary>
    public const string MeterName = "Hali.Signals";

    /// <summary>Counter name — anonymous <c>POST /v1/signals/preview</c> requests.</summary>
    public const string SignalsPreviewRequestsTotalName = "signals_preview_requests_total";

    /// <summary>Counter name — authenticated <c>POST /v1/signals/submit</c> requests.</summary>
    public const string SignalsSubmitRequestsTotalName = "signals_submit_requests_total";

    /// <summary>Histogram name — NLP extraction call duration in seconds.</summary>
    public const string NlpExtractionDurationName = "nlp_extraction_duration_seconds";

    /// <summary>Counter name — clustering / activation outcomes for submitted signals.</summary>
    public const string SignalJoinOutcomeTotalName = "signal_join_outcome_total";

    /// <summary>
    /// Tag key carrying the operational outcome for every instrument owned by
    /// this meter. Values are drawn from the per-instrument catalogs defined
    /// below (<see cref="OutcomeSuccess"/>, <see cref="OutcomeValidationError"/>,
    /// <see cref="OutcomeDependencyError"/> for request counters;
    /// <see cref="NlpOutcomeSuccess"/>, <see cref="NlpOutcomeFallback"/>,
    /// <see cref="NlpOutcomeTimeout"/> for the NLP histogram;
    /// <see cref="JoinOutcomeJoinedExisting"/>, <see cref="JoinOutcomeCreatedNew"/>,
    /// <see cref="JoinOutcomeActivatedCluster"/> for the join counter). A single
    /// shared tag key keeps dashboards uniform and simplifies correlation
    /// queries across the four instruments.
    /// </summary>
    public const string TagOutcome = "outcome";

    // ── Preview / submit request-counter outcomes ────────────────────────────
    // These mirror the three-way wire-visible bucket the issue defined:
    // success (the controller returned a 2xx body), validation_error (user
    // input rejected — ValidationException / ConflictException /
    // RateLimitException / NotFoundException mapped as 4xx), and
    // dependency_error (system/dependency failure — DependencyException,
    // InvariantViolationException, and any other unmapped exception that
    // lands as a 5xx). OperationCanceledException is not counted as any
    // outcome (see SignalsController).

    /// <summary>Outcome tag value: request succeeded (2xx response).</summary>
    public const string OutcomeSuccess = "success";

    /// <summary>Outcome tag value: request rejected due to user input (4xx class).</summary>
    public const string OutcomeValidationError = "validation_error";

    /// <summary>Outcome tag value: request failed due to a dependency / server error (5xx class).</summary>
    public const string OutcomeDependencyError = "dependency_error";

    // ── NLP histogram outcomes ───────────────────────────────────────────────
    // NLP outcomes are distinct from request outcomes because the NLP call
    // can return null (fallback) while the outer request still returns a
    // deterministic response (the ingestion service then throws a bounded
    // DependencyException). The timeout bucket lets operators distinguish
    // slow-upstream failures from hard-upstream failures without reaching
    // for trace data.

    /// <summary>NLP outcome tag value: extraction returned a structured result.</summary>
    public const string NlpOutcomeSuccess = "success";

    /// <summary>
    /// NLP outcome tag value: extraction returned null (HTTP non-2xx,
    /// malformed JSON, disallowed category, or any other error the parser
    /// rejected). Does not indicate the caller request failed — the
    /// ingestion layer maps this to <c>dependency.nlp_unavailable</c>.
    /// </summary>
    public const string NlpOutcomeFallback = "fallback";

    /// <summary>
    /// NLP outcome tag value: the Anthropic call itself did not complete
    /// within the HttpClient timeout (<see cref="System.Threading.Tasks.TaskCanceledException"/>
    /// not caused by the caller cancellation token). Distinguishes upstream
    /// latency regressions from other fallback causes.
    /// </summary>
    public const string NlpOutcomeTimeout = "timeout";

    // ── Join-outcome counter values ──────────────────────────────────────────
    // joined_existing / created_new are emitted from ClusteringService at the
    // decision point (WasCreated = false / true). activated_cluster is emitted
    // from CivisEvaluationService when a cluster transitions from
    // SignalState.Unconfirmed → SignalState.Active — the same event that
    // already emits the ClusterActivated structured log. A single submission
    // can therefore contribute at most one join/create increment plus one
    // (optional) activation increment.

    /// <summary>Join outcome tag value: submitted signal was attached to an existing cluster.</summary>
    public const string JoinOutcomeJoinedExisting = "joined_existing";

    /// <summary>Join outcome tag value: submitted signal created a new cluster.</summary>
    public const string JoinOutcomeCreatedNew = "created_new";

    /// <summary>
    /// Join outcome tag value: cluster transitioned from
    /// <c>unconfirmed</c> → <c>active</c> in the CIVIS evaluation triggered by
    /// this submission. Counted in addition to the joined_existing or
    /// created_new increment for the same signal when activation fires.
    /// </summary>
    public const string JoinOutcomeActivatedCluster = "activated_cluster";

    private readonly Meter _meter;

    /// <summary>
    /// <c>signals_preview_requests_total</c> — incremented exactly once per
    /// <c>POST /v1/signals/preview</c> request, with a single bounded
    /// <see cref="TagOutcome"/> tag. Recorded in <c>SignalsController.Preview</c>
    /// so the counter observes the wire-visible outcome, not internal NLP
    /// behaviour (that is covered separately by
    /// <see cref="NlpExtractionDuration"/>).
    /// </summary>
    public Counter<long> SignalsPreviewRequestsTotal { get; }

    /// <summary>
    /// <c>signals_submit_requests_total</c> — incremented exactly once per
    /// <c>POST /v1/signals/submit</c> request, with a single bounded
    /// <see cref="TagOutcome"/> tag. Recorded in <c>SignalsController.Submit</c>
    /// so the counter observes the wire-visible outcome after idempotency,
    /// rate limiting, validation, spatial derivation, clustering, and CIVIS
    /// evaluation have all run.
    /// </summary>
    public Counter<long> SignalsSubmitRequestsTotal { get; }

    /// <summary>
    /// <c>nlp_extraction_duration_seconds</c> — latency histogram covering the
    /// <c>AnthropicNlpExtractionService.ExtractAsync</c> call. The recorded
    /// span starts immediately before the outbound HTTP send and ends after
    /// the response body is parsed and validated (the only segment the
    /// product actually exposes to users as "composer thinking time"). It
    /// deliberately does not include idempotency / rate-limit / persistence
    /// work from the outer ingestion path — those are covered by the
    /// submit-request counter.
    ///
    /// Tag:
    /// <list type="bullet">
    ///   <item><description><see cref="TagOutcome"/> —
    ///     <see cref="NlpOutcomeSuccess"/> |
    ///     <see cref="NlpOutcomeFallback"/> |
    ///     <see cref="NlpOutcomeTimeout"/>.</description></item>
    /// </list>
    /// </summary>
    public Histogram<double> NlpExtractionDuration { get; }

    /// <summary>
    /// <c>signal_join_outcome_total</c> — counter covering the clustering
    /// decision and the downstream activation transition, tagged only with
    /// <see cref="TagOutcome"/>:
    /// <list type="bullet">
    ///   <item><description><see cref="JoinOutcomeJoinedExisting"/> — emitted
    ///     in <c>ClusteringService.RouteSignalAsync</c> when the scored
    ///     candidate beats the join threshold.</description></item>
    ///   <item><description><see cref="JoinOutcomeCreatedNew"/> — emitted in
    ///     <c>ClusteringService.RouteSignalAsync</c> when no candidate beats
    ///     the threshold.</description></item>
    ///   <item><description><see cref="JoinOutcomeActivatedCluster"/> —
    ///     emitted in <c>CivisEvaluationService.EvaluateClusterAsync</c> when
    ///     MACF + device-diversity gates trip and the cluster transitions to
    ///     <c>Active</c>. Added on top of the join/create increment, so the
    ///     activation rate is directly queryable as
    ///     <c>rate(signal_join_outcome_total{outcome="activated_cluster"})</c>.</description></item>
    /// </list>
    /// No cluster id, locality id, category, or candidate score ever lands in
    /// a tag — the counter is deliberately dimensionless beyond the outcome
    /// bucket to keep dashboards correct at scale.
    /// </summary>
    public Counter<long> SignalJoinOutcomeTotal { get; }

    public SignalsMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        _meter = meterFactory.Create(MeterName);

        SignalsPreviewRequestsTotal = _meter.CreateCounter<long>(
            name: SignalsPreviewRequestsTotalName,
            unit: "{request}",
            description: "Number of POST /v1/signals/preview requests, tagged by wire-visible outcome.");

        SignalsSubmitRequestsTotal = _meter.CreateCounter<long>(
            name: SignalsSubmitRequestsTotalName,
            unit: "{request}",
            description: "Number of POST /v1/signals/submit requests, tagged by wire-visible outcome.");

        NlpExtractionDuration = _meter.CreateHistogram<double>(
            name: NlpExtractionDurationName,
            unit: "s",
            description: "Duration of the Anthropic NLP extraction call and response parse.");

        SignalJoinOutcomeTotal = _meter.CreateCounter<long>(
            name: SignalJoinOutcomeTotalName,
            unit: "{signal}",
            description: "Clustering and activation outcomes for submitted signals.");
    }

    public void Dispose() => _meter.Dispose();
}
